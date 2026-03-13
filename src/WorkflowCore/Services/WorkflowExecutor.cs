using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Models.LifeCycleEvents;

namespace WorkflowCore.Services
{
    /// <summary>
    /// 工作流执行器服务 - WorkflowCore引擎的核心执行组件
    /// 负责管理工作流实例的执行，包括步骤调度、错误处理、生命周期管理等
    /// </summary>
    public class WorkflowExecutor : IWorkflowExecutor
    {
        // 核心依赖服务
        protected readonly IWorkflowRegistry _registry;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly IScopeProvider _scopeProvider;
        protected readonly ILogger _logger;
        private readonly IExecutionResultProcessor _executionResultProcessor;
        private readonly ICancellationProcessor _cancellationProcessor;
        private readonly ILifeCycleEventHub _lifeCycleEventHub;
        private readonly WorkflowOptions _options;

        private IWorkflowHost Host => _serviceProvider.GetService<IWorkflowHost>();

        public WorkflowExecutor(IWorkflowRegistry registry,
        IServiceProvider serviceProvider,
        IScopeProvider scopeProvider,
        IExecutionResultProcessor executionResultProcessor,
        ILifeCycleEventHub lifeCycleEventHub,
        ICancellationProcessor cancellationProcessor,
        WorkflowOptions options,
        ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _scopeProvider = scopeProvider;
            _registry = registry;
            _lifeCycleEventHub = lifeCycleEventHub;
            _cancellationProcessor = cancellationProcessor;
            _options = options;
            _logger = loggerFactory.CreateLogger<WorkflowExecutor>();
            _executionResultProcessor = executionResultProcessor;
        }

        /// <summary>
        /// 执行工作流实例的主入口方法
        /// 这是WorkflowExecutor的核心方法，负责协调整个工作流的执行过程
        /// </summary>
        /// <param name="workflow">要执行的工作流实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工作流执行结果</returns>
        public async Task<WorkflowExecutorResult> Execute(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        {
            // 初始化执行结果容器
            var wfResult = new WorkflowExecutorResult();
            workflow.ExecutionPointers.CompactEndedPointers();
            var now = DateTime.Now;

            // 筛选出需要立即执行的活跃执行指针
            // 条件：1.指针处于活跃状态 2.没有设置睡眠时间或睡眠时间已到
            var exePointers = new List<ExecutionPointer>(workflow.ExecutionPointers.GetRunnablePointers(now));

            // 从注册表获取工作流定义
            var def = _registry.GetDefinition(workflow.WorkflowName, workflow.Version);
            // 验证工作流定义是否存在
            if (def == null)
            {
                _logger.LogError("Workflow {WorkflowDefinitionId} version {Version} is not registered", workflow.WorkflowName, workflow.Version);
                return wfResult;
            }

            // 处理取消请求，检查是否有步骤需要取消
            _cancellationProcessor.ProcessCancellations(workflow, def, wfResult);

            // 遍历所有需要执行的指针，按顺序执行每个步骤
            foreach (var pointer in exePointers)
            {
                // 双重检查指针是否仍然活跃（可能在取消处理中被修改）
                if (!pointer.Active)
                    continue;
                // 根据步骤ID查找对应的工作流步骤定义
                var step = def.Steps.FindById(pointer.StepId);
                // 执行单个步骤的完整流程
                try
                {
                    if (step == null)
                    {
                        throw new Exception($"Unable to find step {pointer.StepId} in workflow definition");
                    }

                    InitializeStep(workflow, step, pointer);

                    await ExecuteStep(workflow, step, pointer, wfResult, def, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Workflow {WorkflowId} raised error on step {StepId} Message: {Message}", workflow.Id, pointer.StepId, ex.Message);

                    wfResult.Errors.Add(new ExecutionError
                    {
                        WorkflowId = workflow.Id,
                        ExecutionPointerId = pointer.Id,
                        ErrorTime = DateTime.Now,
                        Message = ex.Message
                    });

                    Host.ReportStepError(workflow, step, ex);
                    // 调用结果处理器处理异常（触发重试等策略）
                    _executionResultProcessor.HandleStepException(workflow, def, pointer, step, ex);
                }
                _cancellationProcessor.ProcessCancellations(workflow, def, wfResult);
            }

            await DetermineNextExecutionTime(workflow, def);

            using (var scope = _serviceProvider.CreateScope())
            {
                var middlewareRunner = scope.ServiceProvider.GetRequiredService<IWorkflowMiddlewareRunner>();
                await middlewareRunner.RunExecuteMiddleware(workflow, def);
            }

            if(workflow.Status == WorkflowStatus.Complete)
            {
                // 运行工作流完成后的中间件
                using (var scope = _serviceProvider.CreateScope())
                {
                    var middlewareRunner = scope.ServiceProvider.GetRequiredService<IWorkflowMiddlewareRunner>();
                    await middlewareRunner.RunPostMiddleware(workflow, def);
                }
            }

            // Final compaction keeps completed workflows lean and avoids stale ended pointers.
            workflow.ExecutionPointers.CompactEndedPointers();

            return wfResult;
        }

        /// <summary>
        /// 初始化步骤执行
        /// 检查步骤是否可以执行，设置执行状态，发布步骤开始事件
        /// </summary>
        /// <param name="workflow">工作流实例</param>
        /// <param name="step">要初始化的步骤</param>
        /// <param name="pointer">执行指针</param>
        private void InitializeStep(WorkflowInstance workflow, WorkflowStep step, ExecutionPointer pointer)
        {
            // 如果指针尚未处于运行状态，更新状态并发布步骤开始事件
            if (pointer.Status != PointerStatus.Running)
            {
                pointer.Status = PointerStatus.Running;
                // 发布步骤开始事件，通知监听者
                _lifeCycleEventHub.PublishNotification(new WorkStepLifeCycleEvent
                {
                    EventTime = DateTime.Now,
                    ExecutionPointerId = pointer.Id,
                    StepId = step.Id,
                    StepName = pointer.StepName,
                    IsCompleted = false,
                    WorkflowInstanceId = workflow.Id,
                    WorkflowName = workflow.WorkflowName,
                    Version = workflow.Version,
                    WorkflowStatus = workflow.Status,
                });
            }

            // 记录步骤开始时间（如果尚未记录）
            if (!pointer.StartTime.HasValue)
            {
                pointer.StartTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 执行单个工作流步骤的核心方法
        /// 构建执行上下文、创建步骤体、处理输入输出、调用业务逻辑
        /// </summary>
        /// <param name="workflow">工作流实例</param>
        /// <param name="step">要执行的步骤定义</param>
        /// <param name="pointer">执行指针</param>
        /// <param name="wfResult">执行结果容器</param>
        /// <param name="def">工作流定义</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ExecuteStep(WorkflowInstance workflow, WorkflowStep step, ExecutionPointer pointer, WorkflowExecutorResult wfResult, WorkflowDefinition def, CancellationToken cancellationToken = default)
        {
            IStepExecutionContext context = new StepExecutionContext
            {
                Workflow = workflow,
                Step = step,
                PersistenceData = pointer.PersistenceData,
                ExecutionPointer = pointer,
                Item = pointer.ContextItem,
                CancellationToken = cancellationToken
            };

            // 创建步骤执行的依赖注入作用域
            using (var scope = _scopeProvider.CreateScope(context))
            {
                _logger.LogDebug("Starting step {StepName} on workflow {WorkflowId}", step.Name, workflow.Id);

                // 构建步骤体实例，包含具体的业务逻辑
                IStepBody body = step.ConstructBody(scope.ServiceProvider);
                // 获取步骤执行器，负责实际的步骤执行
                var stepExecutor = scope.ServiceProvider.GetRequiredService<IStepExecutor>();

                // 验证步骤体是否成功构建
                if (body == null)
                {
                    throw new Exception($"Unable to construct step body {step.BodyType}");
                }

                // 将工作流数据映射到步骤体的输入属性
                foreach (var input in step.Inputs)
                    input.AssignInput(workflow.Data, body, context);

                await body.BeforRunAsync(context);

                // 执行步骤的核心业务逻辑
                var result = await stepExecutor.ExecuteStep(context, body);

                await body.AfterRunAsync(context);

                // 如果步骤执行成功，将步骤体的输出映射回工作流数据
                if (result.Proceed)
                {
                    foreach (var output in step.Outputs)
                        output.AssignOutput(workflow.Data, body, context);
                }

                // 处理执行结果：更新指针状态、创建后续指针、处理分支等
                _executionResultProcessor.ProcessExecutionResult(workflow, def, pointer, step, result, wfResult);
            }
        }

        /// <summary>
        /// 确定工作流下一次执行时间
        /// 考虑活跃指针、睡眠时间、子指针状态等多个因素
        /// 同时负责判断工作流是否已完成并触发完成事件
        /// </summary>
        /// <param name="workflow">要处理的工作流实例</param>
        /// <param name="def">工作流定义</param>
        /// <returns>异步任务</returns>
        private async Task DetermineNextExecutionTime(WorkflowInstance workflow, WorkflowDefinition def)
        {
            workflow.NextExecution = null;

            if (workflow.Status == WorkflowStatus.Complete)
            {
                return;
            }

            var blockedIds = workflow.ExecutionPointers.GetBlockedPointerIds();
            foreach (var pointer in workflow.ExecutionPointers.Where(x => x.Active && !blockedIds.Contains(x.Id)))
            {
                if (!pointer.SleepUntil.HasValue)
                {
                    workflow.NextExecution = 0;
                    return;
                }

                var pointerSleep = pointer.SleepUntil.Value.Ticks;
                workflow.NextExecution = Math.Min(pointerSleep, workflow.NextExecution ?? pointerSleep);
            }

            // 判断工作流是否完成
            if ((workflow.NextExecution != null) || workflow.ExecutionPointers.Any(x => x.EndTime == null))
            {
                return;
            }

            workflow.Status = WorkflowStatus.Complete;
            workflow.CompleteTime = DateTime.Now;

            _ = _lifeCycleEventHub.PublishNotification(new WorkflowLifeCycleEvent
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
}
