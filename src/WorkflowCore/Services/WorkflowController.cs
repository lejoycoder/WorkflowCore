using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowCore.Exceptions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Models.LifeCycleEvents;

namespace WorkflowCore.Services
{
    public class WorkflowController : IWorkflowController
    {
        private readonly IPersistenceProvider _persistenceStore;
        private readonly IWorkflowRegistry _registry;
        private readonly IQueueProvider _queueProvider;
        private readonly IExecutionPointerFactory _pointerFactory;
        private readonly ILifeCycleEventHub _eventHub;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public WorkflowController(IPersistenceProvider persistenceStore,
        IWorkflowRegistry registry,
        IQueueProvider queueProvider,
        IExecutionPointerFactory pointerFactory,
        ILifeCycleEventHub eventHub,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
        {
            _persistenceStore = persistenceStore;
            _registry = registry;
            _queueProvider = queueProvider;
            _pointerFactory = pointerFactory;
            _eventHub = eventHub;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<WorkflowController>();
        }

        public Task<string> StartWorkflow(string workflowId, object data = null)
        {
            return StartWorkflow(workflowId, null, data);
        }

        public Task<string> StartWorkflow(string workflowId, int? version, object? data = null)
        {
            return StartWorkflow<object>(workflowId, version, data);
        }

        public Task<string> StartWorkflow<TData>(string workflowId, TData? data = null)
            where TData : class
        {
            return StartWorkflow(workflowId, null, data);
        }

        public async Task<string> StartWorkflow<TData>(string workflowId, int? version, TData? data = null)
            where TData : class
        {
            var def = _registry.GetDefinition(workflowId, version) ?? throw new WorkflowNotRegisteredException(workflowId, version);
            var wf = new WorkflowInstance
            {
                WorkflowName = workflowId,
                Version = def.Version,
                Data = data,
                NextExecution = 0,
                CreateTime = DateTime.Now,
                Status = WorkflowStatus.Runnable,
            };

            if (def.DataType != null && data is null)
            {
                wf.Data = _serviceProvider.GetService(def.DataType) ?? def.DataType.GetConstructor([]).Invoke([]);

                if (wf.Data == null)
                    throw new ArgumentException($"Workflow {workflowId} expects data of type {def.DataType}, {typeof(TData)} was provided but could not be instantiated");
            }

            wf.ExecutionPointers.Add(_pointerFactory.BuildGenesisPointer(def));

            using (var scope = _serviceProvider.CreateScope())
            {
                var middlewareRunner = scope.ServiceProvider.GetRequiredService<IWorkflowMiddlewareRunner>();
                await middlewareRunner.RunPreMiddleware(wf, def);
            }

            string id = await _persistenceStore.CreateNewWorkflow(wf);
            await _queueProvider.QueueWork(id, QueueType.Workflow);
            await _eventHub.PublishNotification(new WorkflowLifeCycleEvent
            {
                EventTime = DateTime.Now,
                WorkflowStatus = wf.Status,
                WorkflowInstanceId = id,
                WorkflowName = def.Name,
                Version = def.Version,
                CompleteTime = wf.CompleteTime,
                CreatTime = wf.CreateTime
            });
            return id;
        }

        public async Task PublishEvent(string eventName, string eventKey, object eventData, DateTime? effectiveDate = null)
        {
            _logger.LogDebug("Creating event {EventName} {EventKey}", eventName, eventKey);
            Event evt = new Event();

            if (effectiveDate.HasValue)
                evt.EventTime = effectiveDate.Value;
            else
                evt.EventTime = DateTime.Now;

            evt.EventData = eventData;
            evt.EventKey = eventKey;
            evt.EventName = eventName;
            evt.CreatedTime = DateTime.Now;
            evt.IsProcessed = false;
            string eventId = await _persistenceStore.CreateEvent(evt);

            await _queueProvider.QueueWork(eventId, QueueType.Event);
        }

        public async Task<bool> SuspendWorkflow(string workflowId)
        {
            var wf = await _persistenceStore.GetWorkflowInstance(workflowId);
            if (wf.Status == WorkflowStatus.Runnable)
            {
                wf.Status = WorkflowStatus.Suspended;
                await _persistenceStore.PersistWorkflow(wf);
                await _eventHub.PublishNotification(new WorkflowLifeCycleEvent
                {
                    WorkflowStatus = wf.Status,
                    EventTime = DateTime.Now,
                    WorkflowInstanceId = wf.Id,
                    WorkflowName = wf.WorkflowName,
                    Version = wf.Version,
                    CompleteTime = wf.CompleteTime,
                    CreatTime = wf.CreateTime
                });
                return true;
            }

            return false;

        }

        public async Task<bool> ResumeWorkflow(string workflowId)
        {
            bool requeue = false;
            try
            {
                var wf = await _persistenceStore.GetWorkflowInstance(workflowId);
                if (wf!.Status == WorkflowStatus.Suspended)
                {
                    wf.Status = WorkflowStatus.Runnable;
                    await _persistenceStore.PersistWorkflow(wf);
                    requeue = true;
                    await _eventHub.PublishNotification(new WorkflowLifeCycleEvent
                    {
                        EventTime = DateTime.Now,
                        WorkflowInstanceId = wf.Id,
                        WorkflowStatus = wf.Status,
                        WorkflowName = wf.WorkflowName,
                        Version = wf.Version,
                        CompleteTime = wf.CompleteTime,
                        CreatTime = wf.CreateTime
                    });
                    return true;
                }

                return false;
            }
            finally
            {
                if (requeue)
                    await _queueProvider.QueueWork(workflowId, QueueType.Workflow);
            }
        }

        public async Task<bool> TerminateWorkflow(string workflowId)
        {
            var wf = await _persistenceStore.GetWorkflowInstance(workflowId);

            wf.Status = WorkflowStatus.Terminated;
            wf.CompleteTime = DateTime.Now;

            await _persistenceStore.PersistWorkflow(wf);
            await _eventHub.PublishNotification(new WorkflowLifeCycleEvent
            {
                EventTime = DateTime.Now,
                WorkflowInstanceId = wf.Id,
                WorkflowStatus = wf.Status,
                WorkflowName = wf.WorkflowName,
                Version = wf.Version,
                CompleteTime = wf.CompleteTime,
                CreatTime = wf.CreateTime
            });
            return true;
        }

        public void RegisterWorkflow<TWorkflow>()
            where TWorkflow : IWorkflow
        {
            var wf = ActivatorUtilities.CreateInstance<TWorkflow>(_serviceProvider);
            _registry.RegisterWorkflow(wf);
        }

        public void RegisterWorkflow<TWorkflow, TData>()
            where TWorkflow : IWorkflow<TData>
            where TData : class
        {
            var wf = ActivatorUtilities.CreateInstance<TWorkflow>(_serviceProvider);
            _registry.RegisterWorkflow(wf);
        }
    }
}
