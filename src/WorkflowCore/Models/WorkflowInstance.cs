using System;
using System.Linq;

namespace WorkflowCore.Models
{
    /// <summary>
    /// 工作流实例类，表示一个正在运行或已完成的工作流程实例
    /// </summary>
    public class WorkflowInstance
    {
        /// <summary>
        /// 工作流实例的唯一标识符
        /// </summary>
        public string Id { get; set; }
                
        /// <summary>
        /// 工作流名称Name
        /// </summary>
        public string WorkflowName { get; set; }

        /// <summary>
        /// 工作流定义的版本号
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 执行指针集合，记录工作流中每个步骤的执行状态和位置
        /// </summary>
        public ExecutionPointerCollection ExecutionPointers { get; set; } = new ExecutionPointerCollection();

        /// <summary>
        /// 下次执行时间(Unix时间戳),用于调度延迟执行的工作流
        /// </summary>
        public long? NextExecution { get; set; }

        /// <summary>
        /// 工作流实例的当前状态
        /// </summary>
        public WorkflowStatus Status { get; set; }

        /// <summary>
        /// 工作流实例的数据上下文，存储工作流执行过程中的数据
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// 工作流实例的创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 工作流实例的完成时间，如果尚未完成则为 null
        /// </summary>
        public DateTime? CompleteTime { get; set; }

        /// <summary>
        /// 检查指定父级分支是否已完成
        /// </summary>
        /// <param name="parentId">父级执行指针的标识符</param>
        /// <returns>如果该分支的所有执行指针都已结束则返回 true，否则返回 false</returns>
        public bool IsBranchComplete(string parentId)
        {
            return !ExecutionPointers.HasDescendants(parentId);
        }
    }

    /// <summary>
    /// 工作流状态枚举，定义工作流实例的各种执行状态
    /// </summary>
    public enum WorkflowStatus
    { 
        /// <summary>
        /// 可运行状态,工作流可以继续执行
        /// </summary>
        Runnable = 0,
        /// <summary>
        /// 暂停状态,工作流被暂停执行,等待外部条件满足
        /// </summary>
        Suspended = 1,
        /// <summary>
        /// 完成状态,工作流已成功执行完毕
        /// </summary>
        Complete = 2,
        /// <summary>
        /// 终止状态,工作流被强制终止或因错误而停止
        /// </summary>
        Terminated = 3,
    }
}
