using System;

namespace WorkflowCore.Models
{
    /// <summary>
    /// 工作流事件类，表示工作流系统中的一个事件，用于触发工作流实例的执行或状态变更
    /// </summary>
    public class Event
    {
        /// <summary>
        /// 事件的唯一标识符
        /// </summary>
        public string Id { get; set; }        

        /// <summary>
        /// 事件名称，用于标识事件的类型
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// 事件键，用于进一步细分同类型事件的不同实例
        /// </summary>
        public string EventKey { get; set; }

        /// <summary>
        /// 事件数据，包含事件相关的具体信息和负载数据
        /// </summary>
        public object EventData { get; set; }

        /// <summary>
        /// 事件发生时间，用于确定事件的处理顺序和时效性
        /// </summary>
        public DateTime EventTime { get; set; }

        /// <summary>
        /// 事件创建时间，用于与业务事件时间分离的清理策略
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 事件是否已被处理的标志，用于避免重复处理同一事件
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// 事件最后一次被成功处理的时间
        /// </summary>
        public DateTime? ProcessedTime { get; set; }
    }
}
