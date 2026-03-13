using FluentAssertions;
using WorkflowCore.Models;
using Xunit;

namespace WorkflowCore.UnitTests.Models;

public class WorkflowInstanceTests
{
    [Fact]
    public void IsBranchComplete_should_be_false_when_descendants_still_exist()
    {
        var workflow = new WorkflowInstance();
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "parent",
            StepId = 1,
            Active = false,
            Scope = [],
            Status = PointerStatus.Complete,
            EndTime = DateTime.UtcNow
        });
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "child",
            StepId = 2,
            Active = true,
            Scope = ["parent"],
            Status = PointerStatus.Pending
        });

        workflow.IsBranchComplete("parent").Should().BeFalse();
    }

    [Fact]
    public void IsBranchComplete_should_be_true_when_branch_has_no_descendants()
    {
        var workflow = new WorkflowInstance();
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "parent",
            StepId = 1,
            Active = false,
            Scope = [],
            Status = PointerStatus.Complete,
            EndTime = DateTime.UtcNow
        });

        workflow.IsBranchComplete("parent").Should().BeTrue();
    }

    [Fact]
    public void IsBranchComplete_should_be_true_when_only_ended_descendants_exist()
    {
        var workflow = new WorkflowInstance();
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "parent",
            StepId = 1,
            Active = false,
            Scope = [],
            Status = PointerStatus.Complete,
            EndTime = DateTime.UtcNow
        });
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "child-ended",
            StepId = 2,
            Active = false,
            Scope = ["parent"],
            Status = PointerStatus.Complete,
            EndTime = DateTime.UtcNow
        });

        workflow.IsBranchComplete("parent").Should().BeTrue();
    }
}
