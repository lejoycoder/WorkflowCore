using System;
using System.Threading.Tasks;
using FluentAssertions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Testing;
using Xunit;

namespace WorkflowCore.IntegrationTests.Scenarios
{
    public class LongLoopCompactionScenario : WorkflowTest<LongLoopCompactionScenario.LongLoopWorkflow, LongLoopCompactionScenario.MyDataClass>
    {
        private const int TargetIterations = 300;
        internal static int LoopStepTicker = 0;

        public class MyDataClass
        {
            public int Counter { get; set; }
            public string EventKey { get; set; } = string.Empty;
        }

        public class IncrementStep : StepBody
        {
            public int Counter { get; set; }

            public override ExecutionResult Run(IStepExecutionContext context)
            {
                LoopStepTicker++;
                Counter = LoopStepTicker;
                return ExecutionResult.Next();
            }
        }

        public class LongLoopWorkflow : IWorkflow<MyDataClass>
        {
            public string Name => "LongLoopWorkflow";
            public int Version => 1;

            public void Build(IWorkflowBuilder<MyDataClass> builder)
            {
                builder
                    .StartWith(context => ExecutionResult.Next())
                    .While(data => data.Counter < TargetIterations).Do(then => then
                        .StartWith<IncrementStep>()
                            .Output(data => data.Counter, step => step.Counter))
                    .WaitFor("LongLoopCompactionEvent", data => data.EventKey)
                    .Then(context => ExecutionResult.Next());
            }
        }

        public LongLoopCompactionScenario()
        {
            LoopStepTicker = 0;
            Setup();
        }

        [Fact]
        public async Task Scenario()
        {
            var eventKey = Guid.NewGuid().ToString();
            var workflowId = StartWorkflow(new MyDataClass { Counter = 0, EventKey = eventKey });

            WaitForEventSubscription("LongLoopCompactionEvent", eventKey, TimeSpan.FromSeconds(60));

            LoopStepTicker.Should().Be(TargetIterations);

            var waitingWorkflow = (await PersistenceProvider.GetWorkflowInstance(workflowId))!;
            waitingWorkflow.ExecutionPointers.Count.Should().BeLessOrEqualTo(2);
            waitingWorkflow.ExecutionPointers.Should().OnlyContain(x => x.EndTime == null);

            await Host.PublishEvent("LongLoopCompactionEvent", eventKey, "resume");

            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);
            UnhandledStepErrors.Count.Should().Be(0);

            var completedWorkflow = (await PersistenceProvider.GetWorkflowInstance(workflowId))!;
            completedWorkflow.ExecutionPointers.Count.Should().Be(0);
        }
    }
}
