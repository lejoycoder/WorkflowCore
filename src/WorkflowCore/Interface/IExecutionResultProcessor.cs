using System;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    /// <summary>
    /// 步骤执行结果处理器，负责将步骤结果写回运行时状态。
    /// </summary>
    public interface IExecutionResultProcessor
    {
        /// <summary>
        /// 处理步骤异常并应用错误处理策略（重试、挂起、终止等）。
        /// </summary>
        /// <param name="workflow">工作流实例。</param>
        /// <param name="def">工作流定义。</param>
        /// <param name="pointer">发生异常的执行指针。</param>
        /// <param name="step">发生异常的步骤定义。</param>
        /// <param name="exception">捕获的异常。</param>
        void HandleStepException(WorkflowInstance workflow, WorkflowDefinition def, ExecutionPointer pointer, WorkflowStep step, Exception exception);

        /// <summary>
        /// 处理步骤执行结果并更新指针、分支和工作流状态。
        /// </summary>
        /// <param name="workflow">工作流实例。</param>
        /// <param name="def">工作流定义。</param>
        /// <param name="pointer">当前执行指针。</param>
        /// <param name="step">当前步骤定义。</param>
        /// <param name="result">步骤执行结果。</param>
        /// <param name="workflowResult">本次执行累积结果。</param>
        void ProcessExecutionResult(WorkflowInstance workflow, WorkflowDefinition def, ExecutionPointer pointer, WorkflowStep step, ExecutionResult result, WorkflowExecutorResult workflowResult);
    }
}
