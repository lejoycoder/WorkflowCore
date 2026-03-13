using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    /// <summary>
    /// 工作流执行器，负责驱动单个工作流实例的一轮执行。
    /// </summary>
    public interface IWorkflowExecutor
    {
        /// <summary>
        /// 执行指定工作流实例，并返回本轮执行产生的结果信息。
        /// </summary>
        /// <param name="workflow">待执行的工作流实例。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>执行结果。</returns>
        Task<WorkflowExecutorResult> Execute(WorkflowInstance workflow, CancellationToken cancellationToken = default);
    }
}
