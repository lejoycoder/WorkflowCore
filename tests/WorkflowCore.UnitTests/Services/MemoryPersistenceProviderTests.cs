using FluentAssertions;
using WorkflowCore.Models;
using WorkflowCore.Services;
using Xunit;

namespace WorkflowCore.UnitTests.Services;

public class MemoryPersistenceProviderTests
{
    [Fact]
    public async Task ClearExpiredEvents_should_only_remove_processed_events_that_expired_by_processed_time()
    {
        var provider = new MemoryPersistenceProvider();
        var staleProcessedId = await provider.CreateEvent(new Event
        {
            EventName = "evt",
            EventKey = "a",
            EventData = 1,
            EventTime = new DateTime(2000, 1, 1),
            CreatedTime = DateTime.UtcNow.AddDays(-2),
            IsProcessed = true,
            ProcessedTime = DateTime.UtcNow.AddDays(-2)
        });
        var pendingHistoricId = await provider.CreateEvent(new Event
        {
            EventName = "evt",
            EventKey = "b",
            EventData = 2,
            EventTime = new DateTime(2000, 1, 1),
            CreatedTime = DateTime.UtcNow.AddDays(-2),
            IsProcessed = false
        });
        await provider.ClearExpiredEvents(DateTime.UtcNow.AddHours(-12));

        (await provider.GetEvent(staleProcessedId)).Should().BeNull();
        (await provider.GetEvent(pendingHistoricId)).Should().NotBeNull();
    }

    [Fact]
    public async Task MarkEventProcessed_should_set_processed_time()
    {
        var provider = new MemoryPersistenceProvider();
        var eventId = await provider.CreateEvent(new Event
        {
            EventName = "evt",
            EventKey = "a",
            EventData = 1,
            EventTime = DateTime.UtcNow,
            CreatedTime = DateTime.UtcNow,
            IsProcessed = false
        });

        await provider.MarkEventProcessed(eventId);
        var evt = await provider.GetEvent(eventId);

        evt.Should().NotBeNull();
        evt!.IsProcessed.Should().BeTrue();
        evt.ProcessedTime.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkEventUnprocessed_should_reset_processed_time()
    {
        var provider = new MemoryPersistenceProvider();
        var eventId = await provider.CreateEvent(new Event
        {
            EventName = "evt",
            EventKey = "a",
            EventData = 1,
            EventTime = DateTime.UtcNow,
            CreatedTime = DateTime.UtcNow,
            IsProcessed = false
        });

        await provider.MarkEventProcessed(eventId);
        await provider.MarkEventUnprocessed(eventId);
        var evt = await provider.GetEvent(eventId);

        evt.Should().NotBeNull();
        evt!.IsProcessed.Should().BeFalse();
        evt.ProcessedTime.Should().BeNull();
    }
}
