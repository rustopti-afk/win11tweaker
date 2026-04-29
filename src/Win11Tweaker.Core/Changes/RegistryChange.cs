#if WINDOWS
using Microsoft.Win32;
using Win11Tweaker.Core.Registry;

namespace Win11Tweaker.Core.Changes;

/// <summary>
/// A single registry value modification with full snapshot/rollback support.
/// </summary>
public class RegistryChange : SystemChange
{
    private readonly RegistryService _registry;

    public RegistryHive Hive { get; init; }
    public string KeyPath { get; init; } = string.Empty;
    public string ValueName { get; init; } = string.Empty;
    public object NewValue { get; init; } = 0;
    public RegistryValueKind ValueKind { get; init; }

    public RegistryChange(RegistryService registry)
    {
        _registry = registry;
    }

    public override Task<ChangeSnapshot> CaptureSnapshot()
    {
        var previousValue = _registry.GetValue(Hive, KeyPath, ValueName);
        var previousKind = _registry.GetValueKind(Hive, KeyPath, ValueName);

        return Task.FromResult<ChangeSnapshot>(new RegistrySnapshot
        {
            ChangeId = Id,
            PreviousValue = previousValue,
            PreviousValueKind = previousKind?.ToString(),
            WasAbsent = previousValue == null
        });
    }

    public override Task Apply()
    {
        _registry.SetValue(Hive, KeyPath, ValueName, NewValue, ValueKind);
        return Task.CompletedTask;
    }

    public override Task Rollback(ChangeSnapshot snapshot)
    {
        var reg = (RegistrySnapshot)snapshot;

        if (reg.WasAbsent)
        {
            _registry.DeleteValue(Hive, KeyPath, ValueName);
        }
        else if (reg.PreviousValue != null && reg.PreviousValueKind != null)
        {
            var kind = Enum.Parse<RegistryValueKind>(reg.PreviousValueKind);
            _registry.SetValue(Hive, KeyPath, ValueName, reg.PreviousValue, kind);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Groups multiple registry changes that should be applied and rolled back together.
/// </summary>
public class MultiRegistryChange : SystemChange
{
    private readonly List<RegistryChange> _changes;

    public MultiRegistryChange(List<RegistryChange> changes)
    {
        _changes = changes;
    }

    public override async Task<ChangeSnapshot> CaptureSnapshot()
    {
        var snapshots = new List<ChangeSnapshot>();
        foreach (var change in _changes)
            snapshots.Add(await change.CaptureSnapshot());

        return new MultiSnapshot { ChangeId = Id, Snapshots = snapshots };
    }

    public override async Task Apply()
    {
        foreach (var change in _changes)
            await change.Apply();
    }

    public override async Task Rollback(ChangeSnapshot snapshot)
    {
        var multi = (MultiSnapshot)snapshot;
        // Roll back in reverse order
        for (int i = _changes.Count - 1; i >= 0; i--)
            await _changes[i].Rollback(multi.Snapshots[i]);
    }
}
#endif
