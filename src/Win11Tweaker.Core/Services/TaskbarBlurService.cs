#if WINDOWS
using System.Drawing;
using System.Text.Json;
using Win11Tweaker.Core.WinApi;

namespace Win11Tweaker.Core.Services;

public enum TaskbarBlurMode { None, Blur, Acrylic }

public class TaskbarBlurSettings
{
    public TaskbarBlurMode Mode        { get; set; } = TaskbarBlurMode.None;
    public int             TintR       { get; set; } = 0;
    public int             TintG       { get; set; } = 0;
    public int             TintB       { get; set; } = 0;
    public float           Opacity     { get; set; } = 0.6f;
}

/// <summary>
/// Applies and persists taskbar blur/acrylic effects.
/// Because SetWindowCompositionAttribute is in-memory only, a timer re-applies
/// the effect after Explorer restarts (e.g., after registry tweaks or crashes).
/// </summary>
public class TaskbarBlurService : IDisposable
{
    private readonly string _settingsPath;
    private TaskbarBlurSettings _current = new();
    private System.Threading.Timer? _watchdog;
    private bool _disposed;

    public TaskbarBlurSettings Current => _current;

    public TaskbarBlurService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker", "taskbar_blur.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        LoadSettings();
        StartWatchdog();
    }

    /// <summary>Apply acrylic effect with a tint color and opacity.</summary>
    public void ApplyAcrylic(Color tint, float opacity)
    {
        _current = new TaskbarBlurSettings
        {
            Mode    = TaskbarBlurMode.Acrylic,
            TintR   = tint.R,
            TintG   = tint.G,
            TintB   = tint.B,
            Opacity = Math.Clamp(opacity, 0f, 1f)
        };

        TaskbarBlurApi.ApplyAcrylic(tint, opacity);
        SaveSettings();
    }

    /// <summary>Apply Aero blur (no tint, just frosted glass).</summary>
    public void ApplyBlur()
    {
        _current = new TaskbarBlurSettings { Mode = TaskbarBlurMode.Blur };
        TaskbarBlurApi.ApplyBlur();
        SaveSettings();
    }

    /// <summary>Remove effect — solid taskbar.</summary>
    public void Remove()
    {
        _current = new TaskbarBlurSettings { Mode = TaskbarBlurMode.None };
        TaskbarBlurApi.RemoveEffects();
        SaveSettings();
    }

    /// <summary>Re-apply current settings (called by watchdog).</summary>
    public void Reapply()
    {
        switch (_current.Mode)
        {
            case TaskbarBlurMode.Acrylic:
                var tint = Color.FromArgb(_current.TintR, _current.TintG, _current.TintB);
                TaskbarBlurApi.ApplyAcrylic(tint, _current.Opacity);
                break;
            case TaskbarBlurMode.Blur:
                TaskbarBlurApi.ApplyBlur();
                break;
        }
    }

    // Re-apply every 3 seconds so the effect survives Explorer crashes/restarts
    private void StartWatchdog()
    {
        _watchdog = new System.Threading.Timer(
            _ => { try { Reapply(); } catch { /* Explorer not ready yet */ } },
            null,
            dueTime:  TimeSpan.FromSeconds(2),
            period:   TimeSpan.FromSeconds(3));
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            _current = JsonSerializer.Deserialize<TaskbarBlurSettings>(json) ?? new();
        }
        catch { /* corrupt file — start fresh */ }
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _watchdog?.Dispose();
        _disposed = true;
    }
}
#endif
