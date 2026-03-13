using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    /// <summary>
    /// 执行指针工厂，负责创建工作流运行时所需的各类指针。
    /// </summary>
    public interface IExecutionPointerFactory
    {
        /// <summary>
        /// 创建工作流启动指针（StepId=0）。
        /// </summary>
        /// <param name="def">工作流定义。</param>
        /// <returns>初始执行指针。</returns>
        ExecutionPointer BuildGenesisPointer(WorkflowDefinition def);

        /// <summary>
        /// 基于当前指针和结果出口创建下一个同级指针。
        /// </summary>
        /// <param name="def">工作流定义。</param>
        /// <param name="pointer">当前已执行指针。</param>
        /// <param name="outcomeTarget">匹配到的出口配置。</param>
        /// <returns>下一个待执行指针。</returns>
        ExecutionPointer BuildNextPointer(WorkflowDefinition def, ExecutionPointer pointer, IStepOutcome outcomeTarget);

        /// <summary>
        /// 创建子分支入口指针，并继承父指针上下文与作用域信息。
        /// </summary>
        /// <param name="def">工作流定义。</param>
        /// <param name="pointer">父指针。</param>
        /// <param name="childDefinitionId">子分支入口步骤 Id。</param>
        /// <param name="branch">分支上下文项。</param>
        /// <returns>子分支执行指针。</returns>
        ExecutionPointer BuildChildPointer(WorkflowDefinition def, ExecutionPointer pointer, int childDefinitionId, object branch);
    }
}
