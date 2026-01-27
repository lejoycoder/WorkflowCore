using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services.BackgroundTasks
{
    /// <summary>
    /// 事件消费者，负责处理事件队列中的事件，触发相应的工作流实例继续执行
    /// </summary>
    internal class EventConsumer : QueueConsumer, IBackgroundTask
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IEventRepository _eventRepository;
        protected override int MaxConcurrentItems => 2;
        protected override QueueType Queue => QueueType.Event;

        /// <summary>
        /// 初始化事件消费者
        /// </summary>
        /// <param name="workflowRepository">工作流仓储，用于访问和更新工作流实例</param>
        /// <param name="subscriptionRepository">订阅仓储，用于管理事件订阅</param>
        /// <param name="eventRepository">事件仓储，用于管理事件数据</param>
        /// <param name="queueProvider">队列提供程序</param>
        /// <param name="loggerFactory">日志工厂</param>
        /// <param name="options">工作流配置选项</param>
        public EventConsumer(IWorkflowRepository workflowRepository,
            ISubscriptionRepository subscriptionRepository,
            IEventRepository eventRepository,
            IQueueProvider queueProvider,
            ILoggerFactory loggerFactory,
            WorkflowOptions options)
            : base(queueProvider, loggerFactory, options)
        {
            _workflowRepository = workflowRepository;
            _subscriptionRepository = subscriptionRepository;
            _eventRepository = eventRepository;
        }

        /// <summary>
        /// 处理队列中的事件项，触发相关的工作流实例继续执行
        /// </summary>
        /// <param name="itemId">事件ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        protected override async Task ProcessItem(string itemId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evt = await _eventRepository.GetEvent(itemId, cancellationToken);

            if (evt.IsProcessed)
            {
                return;
            }
            if (evt.EventTime <= DateTime.Now)
            {
                var subs = await _subscriptionRepository.GetSubscriptions(evt.EventName, evt.EventKey, evt.EventTime, cancellationToken);

                var toQueue = new HashSet<string>();
                var complete = true;

                foreach (var sub in subs)
                    complete = await SeedSubscription(evt, sub, toQueue, cancellationToken);

                if (complete)
                {
                    await _eventRepository.MarkEventProcessed(itemId, cancellationToken);
                }

                foreach (var eventId in toQueue)
                    await QueueProvider.QueueWork(eventId, QueueType.Event);

                await _eventRepository.ClearExpiredEvents(DateTime.Now.AddHours(-12));
            }
        }

        /// <summary>
        /// 确保相关事件按正确顺序处理，并触发工作流实例继续执行
        /// </summary>
        /// <param name="evt">当前处理的事件</param>
        /// <param name="sub">事件订阅信息</param>
        /// <param name="toQueue">需要重新排队的事件集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果订阅处理成功返回true，否则返回false</returns>
        private async Task<bool> SeedSubscription(Event evt, EventSubscription sub, HashSet<string> toQueue, CancellationToken cancellationToken)
        {
            foreach (var eventId in await _eventRepository.GetEvents(sub.EventName, sub.EventKey, sub.SubscribeAsOf, cancellationToken))
            {
                if (eventId == evt.Id)
                    continue;

                var siblingEvent = await _eventRepository.GetEvent(eventId, cancellationToken);
                if (siblingEvent.IsProcessed)
                    continue;

                if (siblingEvent.EventTime < evt.EventTime)
                {
                    await QueueProvider.QueueWork(eventId, QueueType.Event);
                    return false;
                }
                toQueue.Add(siblingEvent.Id);
            }

            try
            {
                var workflow = await _workflowRepository.GetWorkflowInstance(sub.WorkflowId, cancellationToken);
                IEnumerable<ExecutionPointer> pointers = null;

                if (!string.IsNullOrEmpty(sub.ExecutionPointerId))
                    pointers = workflow.ExecutionPointers.Where(p => p.Id == sub.ExecutionPointerId && !p.EventPublished && p.EndTime == null);
                else
                    pointers = workflow.ExecutionPointers.Where(p => p.EventName == sub.EventName && p.EventKey == sub.EventKey && !p.EventPublished && p.EndTime == null);

                foreach (var p in pointers)
                {
                    p.EventData = evt.EventData;
                    p.EventPublished = true;
                    p.Active = true;
                }
                workflow.NextExecution = 0;
                await _workflowRepository.PersistWorkflow(workflow, cancellationToken);
                await _subscriptionRepository.TerminateSubscription(sub.Id, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return false;
            }
            finally
            {
                await QueueProvider.QueueWork(sub.WorkflowId, QueueType.Workflow);
            }
        }
    }
}