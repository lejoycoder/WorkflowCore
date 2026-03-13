using FluentAssertions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;
using Xunit;

namespace WorkflowCore.UnitTests.Primitives;

public class ControlPrimitiveTests
{
    [Fact]
    public void OutcomeSwitch_should_emit_branch_with_predecessor_outcome_without_history_lookup()
    {
        var primitive = new OutcomeSwitch();
        var context = CreateContext(new ExecutionPointer
        {
            Id = "current",
            StepId = 2,
            Active = true,
            PredecessorOutcome = "matched",
            Scope = []
        });

        var result = primitive.Run(context);

        result.Proceed.Should().BeFalse();
        result.OutcomeValue.Should().Be("matched");
        result.BranchValues.Should().ContainSingle().Which.Should().BeNull();
        result.PersistenceData.Should().BeOfType<ControlPersistenceData>();
    }

    [Fact]
    public void When_should_branch_when_parent_outcome_matches_expected_outcome()
    {
        var primitive = new When { ExpectedOutcome = 2 };
        var context = CreateContext(new ExecutionPointer
        {
            Id = "when",
            StepId = 3,
            Active = true,
            ParentOutcome = 2,
            Scope = []
        });

        var result = primitive.Run(context);

        result.Proceed.Should().BeFalse();
        result.BranchValues.Should().ContainSingle().Which.Should().BeNull();
        result.PersistenceData.Should().BeOfType<ControlPersistenceData>();
    }

    [Fact]
    public void When_should_skip_branch_when_parent_outcome_does_not_match()
    {
        var primitive = new When { ExpectedOutcome = 1 };
        var context = CreateContext(new ExecutionPointer
        {
            Id = "when",
            StepId = 3,
            Active = true,
            ParentOutcome = 2,
            Scope = []
        });

        var result = primitive.Run(context);

        result.Proceed.Should().BeTrue();
        result.BranchValues.Should().BeEmpty();
    }

    private static IStepExecutionContext CreateContext(ExecutionPointer pointer)
    {
        return new StepExecutionContext
        {
            Workflow = new WorkflowInstance
            {
                Id = "wf",
                WorkflowName = "unit",
                Version = 1,
                Data = new object(),
                Status = WorkflowStatus.Runnable
            },
            Step = new WorkflowStep<TestStepBody> { Id = pointer.StepId, Name = "step" },
            ExecutionPointer = pointer,
            Item = null,
            PersistenceData = null
        };
    }

    private sealed class TestStepBody : StepBody
    {
        public override ExecutionResult Run(IStepExecutionContext context) => ExecutionResult.Next();
    }
}
