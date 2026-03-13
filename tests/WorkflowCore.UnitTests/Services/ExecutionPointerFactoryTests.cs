using FluentAssertions;
using WorkflowCore.Models;
using WorkflowCore.Services;
using Xunit;

namespace WorkflowCore.UnitTests.Services;

public class ExecutionPointerFactoryTests
{
    private readonly ExecutionPointerFactory _factory = new();

    [Fact]
    public void BuildNextPointer_should_carry_predecessor_outcome_and_scope()
    {
        var definition = CreateDefinition();
        var source = new ExecutionPointer
        {
            Id = "source",
            StepId = 0,
            Active = true,
            Scope = ["root"],
            Outcome = 42
        };
        var outcome = new ValueOutcome { NextStep = 1 };

        var next = _factory.BuildNextPointer(definition, source, outcome);

        next.PredecessorId.Should().Be("source");
        next.PredecessorOutcome.Should().Be(42);
        next.Scope.Should().Equal("root");
        next.StepId.Should().Be(1);
    }

    [Fact]
    public void BuildChildPointer_should_set_parent_metadata_and_track_child_on_parent()
    {
        var definition = CreateDefinition();
        var parent = new ExecutionPointer
        {
            Id = "parent",
            StepId = 0,
            Active = true,
            Scope = ["root"],
            Outcome = "branch-result"
        };

        var child = _factory.BuildChildPointer(definition, parent, 2, "item-1");

        child.ParentId.Should().Be("parent");
        child.ParentOutcome.Should().Be("branch-result");
        child.Scope.Should().Equal("parent", "root");
        child.ContextItem.Should().Be("item-1");
        parent.Children.Should().ContainSingle().Which.Should().Be(child.Id);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var definition = new WorkflowDefinition { Name = "unit", Version = 1 };
        definition.Steps.Add(new WorkflowStep<TestStepBody> { Id = 0, Name = "start" });
        definition.Steps.Add(new WorkflowStep<TestStepBody> { Id = 1, Name = "next" });
        definition.Steps.Add(new WorkflowStep<TestStepBody> { Id = 2, Name = "child" });
        return definition;
    }

    private sealed class TestStepBody : StepBody
    {
        public override ExecutionResult Run(Interface.IStepExecutionContext context) => ExecutionResult.Next();
    }
}
