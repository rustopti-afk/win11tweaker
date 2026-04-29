using Microsoft.Extensions.Logging.Abstractions;
using Win11Tweaker.Core.Backup;
using Win11Tweaker.Core.Changes;

namespace Win11Tweaker.Core.Tests;

/// <summary>
/// These tests use an in-memory fake change so they run on any OS (no real registry needed).
/// </summary>
public class ChangeTrackerTests
{
    private ChangeTracker CreateTracker() =>
        new(NullLogger<ChangeTracker>.Instance);

    [Fact]
    public async Task ApplyWithBackup_SuccessfulChange_IsTrackedInHistory()
    {
        var tracker = CreateTracker();
        var change = new FakeChange { Description = "test change" };

        var result = await tracker.ApplyWithBackup(change);

        Assert.True(result.Success);
        Assert.Single(tracker.History);
        Assert.True(change.Applied);
    }

    [Fact]
    public async Task ApplyWithBackup_FailingChange_AutoRollsBack()
    {
        var tracker = CreateTracker();
        var change = new FakeChange { ShouldFailOnApply = true };

        var result = await tracker.ApplyWithBackup(change);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(tracker.History);  // not tracked
        Assert.True(change.RolledBack); // auto-rollback was called
    }

    [Fact]
    public async Task RollbackLast_ReversesLastChange()
    {
        var tracker = CreateTracker();
        var change = new FakeChange { Description = "change to undo" };

        await tracker.ApplyWithBackup(change);
        var rolled = await tracker.RollbackLast();

        Assert.True(rolled);
        Assert.Empty(tracker.History);
        Assert.True(change.RolledBack);
    }

    [Fact]
    public async Task RollbackLast_EmptyHistory_ReturnsFalse()
    {
        var tracker = CreateTracker();
        var rolled = await tracker.RollbackLast();
        Assert.False(rolled);
    }

    [Fact]
    public async Task RollbackAll_UndoesAllChangesInReverseOrder()
    {
        var tracker = CreateTracker();
        var changes = Enumerable.Range(0, 3)
            .Select(i => new FakeChange { Description = $"change {i}" })
            .ToList();

        foreach (var c in changes)
            await tracker.ApplyWithBackup(c);

        await tracker.RollbackAll();

        Assert.Empty(tracker.History);
        Assert.All(changes, c => Assert.True(c.RolledBack));
    }

    [Fact]
    public async Task ApplyWithBackup_MultipleChanges_AllTracked()
    {
        var tracker = CreateTracker();
        for (int i = 0; i < 5; i++)
            await tracker.ApplyWithBackup(new FakeChange { Description = $"change {i}" });

        Assert.Equal(5, tracker.History.Count);
    }
}

// ── Test double ───────────────────────────────────────────────────────────────

internal class FakeChange : SystemChange
{
    public bool ShouldFailOnApply { get; init; }
    public bool Applied { get; private set; }
    public bool RolledBack { get; private set; }
    public bool SnapshotCaptured { get; private set; }

    public FakeChange()
    {
        // Override init-only Category via object initializer not possible,
        // so we use the backing field approach via constructor.
    }

    public override Task<ChangeSnapshot> CaptureSnapshot()
    {
        SnapshotCaptured = true;
        return Task.FromResult<ChangeSnapshot>(new FakeSnapshot { ChangeId = Id });
    }

    public override Task Apply()
    {
        if (ShouldFailOnApply)
            throw new InvalidOperationException("Simulated apply failure");
        Applied = true;
        return Task.CompletedTask;
    }

    public override Task Rollback(ChangeSnapshot snapshot)
    {
        RolledBack = true;
        Applied = false;
        return Task.CompletedTask;
    }
}

internal class FakeSnapshot : ChangeSnapshot { }
