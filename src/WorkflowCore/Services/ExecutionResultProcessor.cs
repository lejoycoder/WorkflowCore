using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Models.LifeCycleEvents;

namespace WorkflowCore.Services
{
    public class ExecutionResultProcessor : IExecutionResultProcessor
    {
        private readonly IExecutionPointerFactory _pointerFactory;
        private readonly ILifeCycleEventHub ILifeCycleEventHub;
        private readonly IEnumerable<IWorkflowErrorHandler> _errorHandlers;

        public ExecutionResultProcessor(IExecutionPointerFactory pointerFactory, ILifeCycleEventHub eventPublisher, IEnumerable<IWorkflowErrorHandler> errorHandlers)
        {
            _pointerFactory = pointerFactory;
            ILifeCycleEventHub = eventPublisher;
            _errorHandlers = errorHandlers;
        }

        public void ProcessExecutionResult(WorkflowInstance workflow, WorkflowDefinition def, ExecutionPointer pointer, WorkflowStep step, ExecutionResult result, WorkflowExecutorResult workflowResult)
        {
            pointer.PersistenceData = result.PersistenceData;
            pointer.Outcome = result.OutcomeValue;
            if (result.SleepFor.HasValue)
            {
                pointer.SleepUntil = DateTime.Now.Add(result.SleepFor.Value);
                pointer.Status = PointerStatus.Sleeping;
            }

            if (!string.IsNullOrEmpty(result.EventName))
            {
                pointer.EventName = result.EventName;
                pointer.EventKey = result.EventKey;
                pointer.Active = false;
                pointer.Status = PointerStatus.WaitingForEvent;

                workflowResult.Subscriptions.Add(new EventSubscription
                {
                    WorkflowId = workflow.Id,
                    StepId = pointer.StepId,
                    ExecutionPointerId = pointer.Id,
                    EventName = pointer.EventName,
                    EventKey = pointer.EventKey,
                    SubscribeAsOf = result.EventAsOf,
                    SubscriptionData = result.SubscriptionData
                });
            }

            if (result.Proceed)
            {
                pointer.Active = false;
                pointer.EndTime = DateTime.Now;
                pointer.Status = PointerStatus.Complete;

                foreach (var outcomeTarget in step.Outcomes.Where(x => x.Matches(result, workflow.Data)))
                {
                    workflow.ExecutionPointers.Add(_pointerFactory.BuildNextPointer(def, pointer, outcomeTarget));
                }

                ILifeCycleEventHub.PublishNotification(new WorkStepLifeCycleEvent
                {
                    EventTime = DateTime.Now,
                    ExecutionPointerId = pointer.Id,
                    StepName = pointer.StepName,
                    WorkflowStatus = workflow.Status,
                    IsCompleted = true,
                    Elapsed = pointer.Elapsed,
                    StepId = step.Id,
                    WorkflowInstanceId = workflow.Id,
                    WorkflowName = workflow.WorkflowName,
                    Version = workflow.Version
                });
            }
            else
            {
                foreach (var branch in result.BranchValues)
                {
                    foreach (var childDefId in step.Children)
                    {
                        workflow.ExecutionPointers.Add(_pointerFactory.BuildChildPointer(def, pointer, childDefId, branch));
                    }
                }
            }

            if (result.DesiredWorkflowStatus != workflow.Status)
            {
                workflow.Status = result.DesiredWorkflowStatus;

                if (workflow.Status == WorkflowStatus.Complete)
                    workflow.CompleteTime = DateTime.Now;

                ILifeCycleEventHub.PublishNotification(new WorkflowLifeCycleEvent
                {
                    EventTime = DateTime.Now,
                    WorkflowInstanceId = workflow.Id,
                    WorkflowName = workflow.WorkflowName,
                    Version = workflow.Version,
                    WorkflowStatus = workflow.Status,
                    CompleteTime = workflow.CompleteTime,
                    CreatTime = workflow.CreateTime
                });
            }
        }

        public void HandleStepException(WorkflowInstance workflow, WorkflowDefinition def, ExecutionPointer pointer, WorkflowStep step, Exception exception)
        {
            pointer.Status = PointerStatus.Failed;

            var queue = new Queue<ExecutionPointer>();
            queue.Enqueue(pointer);

            while (queue.Count > 0)
            {
                var exceptionPointer = queue.Dequeue();
                var exceptionStep = def.Steps.FindById(exceptionPointer.StepId);
                var errorOption = exceptionStep.ErrorBehavior ?? def.DefaultErrorBehavior;

                foreach (var handler in _errorHandlers.Where(x => x.Type == errorOption))
                {
                    handler.Handle(workflow, def, exceptionPointer, exceptionStep, exception, queue);
                }
            }
        }
    }
}