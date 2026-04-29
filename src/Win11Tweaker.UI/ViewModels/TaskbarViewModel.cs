using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.ViewModels;

public class TaskbarViewModel : BaseViewModel
{
    private readonly TaskbarTweakService _service;

    public TaskbarViewModel(TaskbarTweakService service)
    {
        _service = service;
    }

    // ── Search button ─────────────────────────────────────────────────────────
    private bool _showSearch = true;
    public bool ShowSearch
    {
        get => _showSearch;
        set
        {
            if (!Set(ref _showSearch, value)) return;
            Apply(() => _service.SetSearchButtonVisible(value),
                  value ? "Search button shown" : "Search button hidden");
        }
    }

    // ── Widgets button ────────────────────────────────────────────────────────
    private bool _showWidgets = true;
    public bool ShowWidgets
    {
        get => _showWidgets;
        set
        {
            if (!Set(ref _showWidgets, value)) return;
            Apply(() => _service.SetWidgetsButtonVisible(value),
                  value ? "Widgets button shown" : "Widgets button hidden");
        }
    }

    // ── Chat button ───────────────────────────────────────────────────────────
    private bool _showChat = true;
    public bool ShowChat
    {
        get => _showChat;
        set
        {
            if (!Set(ref _showChat, value)) return;
            Apply(() => _service.SetChatButtonVisible(value),
                  value ? "Chat button shown" : "Chat button hidden");
        }
    }

    // ── Alignment ─────────────────────────────────────────────────────────────
    private bool _centerAligned = true;
    public bool CenterAligned
    {
        get => _centerAligned;
        set
        {
            if (!Set(ref _centerAligned, value)) return;
            var alignment = value ? TaskbarAlignment.Center : TaskbarAlignment.Left;
            Apply(() => _service.SetTaskbarAlignment(alignment),
                  value ? "Taskbar aligned: Center" : "Taskbar aligned: Left");
        }
    }

    // ── Small icons ───────────────────────────────────────────────────────────
    private bool _smallIcons;
    public bool SmallIcons
    {
        get => _smallIcons;
        set
        {
            if (!Set(ref _smallIcons, value)) return;
            Apply(() => _service.SetSmallIcons(value),
                  value ? "Small taskbar icons" : "Normal taskbar icons");
        }
    }

    // ── Auto-hide ─────────────────────────────────────────────────────────────
    private bool _autoHide;
    public bool AutoHide
    {
        get => _autoHide;
        set
        {
            if (!Set(ref _autoHide, value)) return;
            Apply(() => _service.SetAutoHide(value),
                  value ? "Auto-hide enabled" : "Auto-hide disabled");
        }
    }

    // ── Dark mode ─────────────────────────────────────────────────────────────
    private bool _darkMode = true;
    public bool DarkMode
    {
        get => _darkMode;
        set
        {
            if (!Set(ref _darkMode, value)) return;
            Apply(() => _service.SetDarkMode(value),
                  value ? "Dark mode enabled" : "Light mode enabled");
        }
    }

    // ── Transparency ──────────────────────────────────────────────────────────
    private bool _transparency = true;
    public bool Transparency
    {
        get => _transparency;
        set
        {
            if (!Set(ref _transparency, value)) return;
            Apply(() => _service.SetTransparencyEnabled(value),
                  value ? "Transparency effects on" : "Transparency effects off");
        }
    }

    // ── Shared apply helper ───────────────────────────────────────────────────
    private async void Apply(Func<System.Threading.Tasks.Task<Win11Tweaker.Core.Backup.ApplyResult>> action,
        string successMessage)
    {
        IsBusy = true;
        try
        {
            var result = await action();
            SetStatus(result.Success ? $"✓ {successMessage}" : $"✗ {result.Error}");
            UpdateChangesCount();
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
