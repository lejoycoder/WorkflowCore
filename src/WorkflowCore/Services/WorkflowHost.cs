using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Models.LifeCycleEvents;

namespace WorkflowCore.Services
{
    public class WorkflowHost : IWorkflowHost, IDisposable
    {
        protected bool _shutdown = true;
        protected readonly IServiceProvider _serviceProvider;

        private readonly IEnumerable<IBackgroundTask> _backgroundTasks;
        private readonly IWorkflowController _workflowController;

        public event StepErrorEventHandler OnStepError;
        public event LifeCycleEventHandler OnLifeCycleEvent;

        // Public dependencies to allow for extension method access.
        public IPersistenceProvider PersistenceStore { get; private set; }
        public IWorkflowRegistry Registry { get; private set; }
        public WorkflowOptions Options { get; private set; }
        public IQueueProvider QueueProvider { get; private set; }
        public ILogger Logger { get; private set; }

        private readonly ILifeCycleEventHub _lifeCycleEventHub;

        public WorkflowHost(IPersistenceProvider persistenceStore,
            IQueueProvider queueProvider,
            WorkflowOptions options,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IWorkflowRegistry registry,
            IEnumerable<IBackgroundTask> backgroundTasks,
            IWorkflowController workflowController,
            ILifeCycleEventHub lifeCycleEventHub)
        {
            PersistenceStore = persistenceStore;
            QueueProvider = queueProvider;
            Options = options;
            Logger = loggerFactory.CreateLogger<WorkflowHost>();
            _serviceProvider = serviceProvider;
            Registry = registry;
            _backgroundTasks = backgroundTasks;
            _workflowController = workflowController;
            _lifeCycleEventHub = lifeCycleEventHub;
        }

        public Task<string> StartWorkflow(string workflowId, object? data = null)
        {
            return _workflowController.StartWorkflow(workflowId, data);
        }

        public Task<string> StartWorkflow(string workflowId, int? version, object? data = null)
        {
            return _workflowController.StartWorkflow<object>(workflowId, version, data);
        }

        public Task<string> StartWorkflow<TData>(string workflowId, TData? data = null)
            where TData : class
        {
            return _workflowController.StartWorkflow<TData>(workflowId, null, data);
        }

        public Task<string> StartWorkflow<TData>(string workflowId, int? version, TData? data = null)
            where TData : class
        {
            return _workflowController.StartWorkflow(workflowId, version, data);
        }

        public Task PublishEvent(string eventName, string eventKey, object eventData, DateTime? effectiveDate = null)
        {
            return _workflowController.PublishEvent(eventName, eventKey, eventData, effectiveDate);
        }

        public void Start()
        {
            StartAsync(CancellationToken.None).Wait();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _shutdown = false;
            await QueueProvider.Start();
            await _lifeCycleEventHub.Start();

            // Event subscriptions are removed when stopping the event hub.
            // Add them when starting.
            AddEventSubscriptions();

            Logger.LogInformation("Starting background tasks");

            foreach (var task in _backgroundTasks)
                task.Start();
        }

        public void Stop()
        {
            StopAsync(CancellationToken.None).Wait();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdown = true;

            Logger.LogInformation("Stopping background tasks");
            foreach (var th in _backgroundTasks)
                th.Stop();

            Logger.LogInformation("Worker tasks stopped");

            await QueueProvider.Stop();
            await _lifeCycleEventHub.Stop();
        }

        public void RegisterWorkflow<TWorkflow>()
            where TWorkflow : IWorkflow
        {
            _workflowController.RegisterWorkflow<TWorkflow>();
        }

        public void RegisterWorkflow<TWorkflow, TData>()
            where TWorkflow : IWorkflow<TData>
            where TData : class
        {
            _workflowController.RegisterWorkflow<TWorkflow, TData>();
        }

        public Task<bool> SuspendWorkflow(string workflowId)
        {
            return _workflowController.SuspendWorkflow(workflowId);
        }

        public Task<bool> ResumeWorkflow(string workflowId)
        {
            return _workflowController.ResumeWorkflow(workflowId);
        }

        public Task<bool> TerminateWorkflow(string workflowId)
        {
            return _workflowController.TerminateWorkflow(workflowId);
        }

        public void HandleLifeCycleEvent(LifeCycleEvent evt)
        {
            OnLifeCycleEvent?.Invoke(evt);
        }

        public void ReportStepError(WorkflowInstance workflow, WorkflowStep step, Exception exception)
        {
            try
            {
                OnStepError?.Invoke(workflow, step, exception);
            }
            catch (Exception ex)
            {
                Logger.LogError("自动化流程报告错误出错:" +ex.Message);
            }
        }

        public void Dispose()
        {
            if (!_shutdown)
                Stop();
        }

        private void AddEventSubscriptions()
        {
            _lifeCycleEventHub.Subscribe(HandleLifeCycleEvent);
        }
    }
}
