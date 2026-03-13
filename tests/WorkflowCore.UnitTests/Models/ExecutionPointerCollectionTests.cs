using FluentAssertions;
using WorkflowCore.Models;
using Xunit;

namespace WorkflowCore.UnitTests.Models;

public class ExecutionPointerCollectionTests
{
    [Fact]
    public void GetRunnablePointers_should_only_return_active_leaf_pointers_ready_to_run()
    {
        var collection = new ExecutionPointerCollection();
        var parent = CreatePointer("parent", 1, active: true);
        var runnableChild = CreatePointer("child", 2, active: true, scope: ["parent"]);
        var sleepingChild = CreatePointer("sleeping", 3, active: true, sleepUntil: DateTime.UtcNow.AddMinutes(2));
        var inactiveLeaf = CreatePointer("inactive", 4, active: false);

        parent.Children.Add(runnableChild.Id);

        collection.Add(parent);
        collection.Add(runnableChild);
        collection.Add(sleepingChild);
        collection.Add(inactiveLeaf);

        var runnable = collection.GetRunnablePointers(DateTime.UtcNow);

        runnable.Should().ContainSingle()
            .Which.Id.Should().Be("child");
    }

    [Fact]
    public void CompactEndedPointers_should_remove_completed_entries_and_detach_them_from_parent_children()
    {
        var collection = new ExecutionPointerCollection();
        var parent = CreatePointer("parent", 1, active: true);
        var endedChild = CreatePointer("ended", 2, active: false, endTime: DateTime.UtcNow, parentId: "parent", scope: ["parent"]);
        var activeChild = CreatePointer("active", 3, active: true, parentId: "parent", scope: ["parent"]);

        parent.Children.AddRange([endedChild.Id, activeChild.Id]);

        collection.Add(parent);
        collection.Add(endedChild);
        collection.Add(activeChild);

        collection.CompactEndedPointers();

        collection.FindById("ended").Should().BeNull();
        collection.FindById("active").Should().NotBeNull();
        parent.Children.Should().Equal("active");
    }

    [Fact]
    public void HasDescendants_should_follow_scope_entries()
    {
        var collection = new ExecutionPointerCollection();
        collection.Add(CreatePointer("parent", 1, active: true));
        collection.Add(CreatePointer("child", 2, active: true, scope: ["parent"]));

        collection.HasDescendants("parent").Should().BeTrue();
        collection.HasDescendants("missing").Should().BeFalse();
    }

    [Fact]
    public void HasDescendants_should_ignore_ended_descendants()
    {
        var collection = new ExecutionPointerCollection();
        collection.Add(CreatePointer("parent", 1, active: true));
        collection.Add(CreatePointer("ended", 2, active: false, scope: ["parent"], endTime: DateTime.UtcNow));

        collection.HasDescendants("parent").Should().BeFalse();
    }

    private static ExecutionPointer CreatePointer(
        string id,
        int stepId,
        bool active,
        IReadOnlyCollection<string>? scope = null,
        DateTime? sleepUntil = null,
        DateTime? endTime = null,
        string? parentId = null)
    {
        return new ExecutionPointer
        {
            Id = id,
            StepId = stepId,
            Active = active,
            Scope = scope ?? [],
            SleepUntil = sleepUntil,
            EndTime = endTime,
            ParentId = parentId,
            Status = active ? PointerStatus.Pending : PointerStatus.Complete
        };
    }
}
