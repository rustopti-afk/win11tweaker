using System.Text.Json.Serialization;

namespace Win11Tweaker.Core.Changes;

public enum RiskLevel { Safe, Caution, Dangerous, Critical }

/// <summary>
/// Base class for every system modification the app can make.
/// Each change knows how to snapshot its current state, apply itself, and roll back.
/// </summary>
public abstract class SystemChange
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Safe;
    public bool RequiresExplorerRestart { get; init; }
    public bool RequiresLogoff { get; init; }

    public abstract Task<ChangeSnapshot> CaptureSnapshot();
    public abstract Task Apply();
    public abstract Task Rollback(ChangeSnapshot snapshot);
}

/// <summary>
/// Snapshot of the system state before a change was applied.
/// Serialized to disk so rollback survives a crash.
/// </summary>
[JsonDerivedType(typeof(RegistrySnapshot), "registry")]
[JsonDerivedType(typeof(FileSnapshot), "file")]
[JsonDerivedType(typeof(MultiSnapshot), "multi")]
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
public abstract class ChangeSnapshot
{
    public string ChangeId { get; set; } = string.Empty;
}

public class RegistrySnapshot : ChangeSnapshot
{
    public object? PreviousValue { get; set; }
    public string? PreviousValueKind { get; set; }
    public bool WasAbsent { get; set; }
}

public class FileSnapshot : ChangeSnapshot
{
    public string OriginalPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public bool WasAbsent { get; set; }
}

public class MultiSnapshot : ChangeSnapshot
{
    public List<ChangeSnapshot> Snapshots { get; set; } = [];
}
