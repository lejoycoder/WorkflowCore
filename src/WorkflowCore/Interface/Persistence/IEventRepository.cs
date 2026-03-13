using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    /// <summary>
    /// 事件存储库接口，提供事件的持久化操作
    /// </summary>
    public interface IEventRepository
    {
        /// <summary>
        /// 创建新事件
        /// </summary>
        /// <param name="newEvent">新事件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事件ID</returns>
        Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据ID获取事件
        /// </summary>
        /// <param name="id">事件ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事件实例</returns>
        Task<Event> GetEvent(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取可运行的事件ID列表
        /// </summary>
        /// <param name="asAt">截止时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>可运行事件ID集合</returns>
        Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据条件获取事件ID列表
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="eventKey">事件键</param>
        /// <param name="asOf">时间点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事件ID集合</returns>
        Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记事件为已处理
        /// </summary>
        /// <param name="id">事件ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task MarkEventProcessed(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记事件为未处理
        /// </summary>
        /// <param name="id">事件ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理已过期事件。
        /// 仅应清理已处理事件，且以事件处理时间作为过期判断依据。
        /// </summary>
        /// <param name="asAt">过期截止时间。</param>
        /// <param name="_">取消令牌。</param>
        /// <returns>异步任务。</returns>
        Task ClearExpiredEvents(DateTime asAt, CancellationToken _ = default);
    }
}
