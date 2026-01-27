using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    public class MemoryPersistenceProvider : IPersistenceProvider
    {
        private readonly List<WorkflowInstance> _instances = new List<WorkflowInstance>();
        private readonly List<EventSubscription> _subscriptions = new List<EventSubscription>();
        private readonly List<Event> _events = new List<Event>();

        public async Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken _ = default)
        {
            lock (_instances)
            {
                workflow.Id = Guid.NewGuid().ToString();
                _instances.Add(workflow);
                return workflow.Id;
            }
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken _ = default)
        {
            lock (_instances)
            {
                var existing = _instances.First(x => x.Id == workflow.Id);
                _instances.Remove(existing);
                _instances.Add(workflow);
            }
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
        {
            lock (_instances)
            {
                var existing = _instances.First(x => x.Id == workflow.Id);
                _instances.Remove(existing);
                _instances.Add(workflow);

                lock (_subscriptions)
                {
                    foreach (var subscription in subscriptions)
                    {
                        subscription.Id = Guid.NewGuid().ToString();
                        _subscriptions.Add(subscription);
                    }
                }
            }
        }

        public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken _ = default)
        {
            lock (_instances)
            {
                var now = asAt.Ticks;
                return _instances.Where(x => x.NextExecution.HasValue && x.NextExecution <= now).Select(x => x.Id).ToList();
            }
        }

        public async Task<WorkflowInstance?> GetWorkflowInstance(string Id, CancellationToken _ = default)
        {
            lock (_instances)
            {
                return _instances.Single(x => x.Id == Id);
            }
        }

        public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken _ = default)
        {
            if (ids == null)
            {
                return new List<WorkflowInstance>();
            }

            lock (_instances)
            {
                return _instances.Where(x => ids.Contains(x.Id));
            }
        }

        public async Task<List<WorkflowInstance>> FindWorkflowByDefinitionId(string workflowName,CancellationToken cancellationToken = default)
        {
            lock (_instances)
            {
                return [.. _instances.Where(x => x.WorkflowName == workflowName)];
            }
        }

        public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken _ = default)
        {
            lock (_subscriptions)
            {
                subscription.Id = Guid.NewGuid().ToString();
                _subscriptions.Add(subscription);
                return subscription.Id;
            }
        }

        public async Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken _ = default)
        {
            lock (_subscriptions)
            {
                return _subscriptions
                    .Where(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf);
            }
        }

        public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken _ = default)
        {
            lock (_subscriptions)
            {
                var sub = _subscriptions.Single(x => x.Id == eventSubscriptionId);
                _subscriptions.Remove(sub);
            }
        }

        public Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken _ = default)
        {
            lock (_subscriptions)
            {
                var sub = _subscriptions.Single(x => x.Id == eventSubscriptionId);
                return Task.FromResult(sub);
            }
        }

        public Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken _ = default)
        {
            lock (_subscriptions)
            {
                var result =  _subscriptions
                    .FirstOrDefault(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf);
                return Task.FromResult(result);
            }
        }

        public async Task<string> CreateEvent(Event newEvent, CancellationToken _ = default)
        {
            lock (_events)
            {
                newEvent.Id = Guid.NewGuid().ToString();
                _events.Add(newEvent);
                return newEvent.Id;
            }
        }

        public async Task MarkEventProcessed(string id, CancellationToken _ = default)
        {
            lock (_events)
            {
                var evt = _events.FirstOrDefault(x => x.Id == id);
                if (evt != null)
                {
                    evt.IsProcessed = true;
                    //_events.Remove(evt);
                }
            }
        }

        public async Task ClearExpiredEvents(DateTime asAt, CancellationToken _ = default)
        {
            lock (_events)
            {
                _events.RemoveAll(x => x.EventTime < asAt);
            }
        }

        public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken _ = default)
        {
            lock (_events)
            {
                return _events
                    .Where(x => !x.IsProcessed)
                    .Where(x => x.EventTime <= asAt)
                    .Select(x => x.Id)
                    .ToList();
            }
        }

        public async Task<Event> GetEvent(string id, CancellationToken _ = default)
        {
            lock (_events)
            {
                return _events.FirstOrDefault(x => x.Id == id);
            }
        }

        public async Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken _ = default)
        {
            lock (_events)
            {
                return _events
                    .Where(x => x.EventName == eventName && x.EventKey == eventKey)
                    .Where(x => x.EventTime >= asOf)
                    .Select(x => x.Id)
                    .ToList();
            }
        }

        public async Task MarkEventUnprocessed(string id, CancellationToken _ = default)
        {
            lock (_events)
            {
                var evt = _events.FirstOrDefault(x => x.Id == id);
                if (evt != null)
                {
                    evt.IsProcessed = false;
                }
            }
        }
    }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
