using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkflowCore.Models
{
    /// <summary>
    /// 执行指针类，表示工作流中单个步骤的执行状态和上下文信息
    /// </summary>
    public class ExecutionPointer
    {
        private IReadOnlyCollection<string> _scope = new List<string>();

        /// <summary>
        /// 指针的唯一标识符
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// 步骤ID，指向工作流定义中的具体步骤
        /// </summary>
        public int StepId { get; set; }

        /// <summary>
        /// 指示此指针是否活跃（正在执行或等待执行）
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// 睡眠直到指定时间，用于延迟执行
        /// </summary>
        public DateTime? SleepUntil { get; set; }

        /// <summary>
        /// 持久化数据，用于在步骤之间保存状态信息
        /// </summary>
        public object? PersistenceData { get; set; }

        /// <summary>
        /// 步骤开始执行时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 步骤结束执行时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 等待的事件名称
        /// </summary>
        public string? EventName { get; set; }

        /// <summary>
        /// 等待的事件键值
        /// </summary>
        public string? EventKey { get; set; }

        /// <summary>
        /// 指示等待的事件是否已被发布
        /// </summary>
        public bool EventPublished { get; set; }

        /// <summary>
        /// 事件数据，存储接收到的事件载荷
        /// </summary>
        public object? EventData { get; set; }

        /// <summary>
        /// 扩展属性字典，用于存储自定义属性
        /// </summary>
        public Dictionary<string, object> ExtensionAttributes { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 步骤显示名称，用于日志和调试
        /// </summary>
        public string? StepName { get; set; }

        /// <summary>
        /// 重试次数，记录步骤失败后的重试次数
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// 子指针ID列表，用于表示并行执行的分支
        /// </summary>
        public List<string> Children { get; set; } = new List<string>();

        /// <summary>
        /// 直接父分支指针ID，仅对子分支入口节点设置
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// 上下文项，在 ForEach 循环中保存当前处理的项目
        /// </summary>
        public object? ContextItem { get; set; }

        /// <summary>
        /// 前驱指针ID，用于跟踪步骤依赖关系
        /// </summary>
        public string? PredecessorId { get; set; }

        /// <summary>
        /// 前驱步骤的结果，用于在清理历史指针后继续传递结果
        /// </summary>
        public object? PredecessorOutcome { get; set; }

        /// <summary>
        /// 父分支步骤的结果，用于控制分支在清理历史指针后继续工作
        /// </summary>
        public object? ParentOutcome { get; set; }

        /// <summary>
        /// 步骤执行结果，用于决定下一个执行的分支
        /// </summary>
        public object? Outcome { get; set; }

        /// <summary>
        /// 指针状态，指示当前执行指针的状态
        /// </summary>
        public PointerStatus Status { get; set; } = PointerStatus.Legacy;

        /// <summary>
        /// 作用域,定义步骤的执行作用域(其主分支)
        /// </summary>
        public IReadOnlyCollection<string> Scope
        {
            get => _scope;
            set => _scope = new List<string>(value);
        }
        
        public TimeSpan Elapsed => (StartTime.HasValue && EndTime.HasValue) ? EndTime.Value - StartTime.Value : TimeSpan.Zero;
    }

    /// <summary>
    /// 执行指针状态枚举，定义工作流步骤的各种执行状态
    /// </summary>
    public enum PointerStatus
    {
        /// <summary>传统状态（兼容性）</summary>
        Legacy = 0,
        /// <summary>等待执行</summary>
        Pending = 1,
        /// <summary>正在运行</summary>
        Running = 2,
        /// <summary>已完成</summary>
        Complete = 3,
        /// <summary>睡眠中（延时执行）</summary>
        Sleeping = 4,
        /// <summary>等待事件</summary>
        WaitingForEvent = 5,
        /// <summary>执行失败</summary>
        Failed = 6,
        /// <summary>已取消</summary>
        Cancelled = 8,
    }
}
