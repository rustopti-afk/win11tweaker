using System.Windows.Media;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.ViewModels;

public class AppearanceViewModel : BaseViewModel
{
    private readonly TaskbarTweakService _service;

    public AppearanceViewModel(TaskbarTweakService service)
    {
        _service = service;
    }

    private bool _showOnTaskbar;
    public bool ShowColorOnTaskbar
    {
        get => _showOnTaskbar;
        set
        {
            if (!Set(ref _showOnTaskbar, value)) return;
            Apply(() => _service.SetColorPrevalence(value),
                  value ? "Accent color shown on taskbar" : "Accent color hidden from taskbar");
        }
    }

    private Color _accentColor = Color.FromRgb(0, 120, 212);
    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            if (!Set(ref _accentColor, value)) return;
            // Convert WPF Color to ARGB int (Windows uses 0xAARRGGBB)
            int colorInt = (value.A << 24) | (value.R << 16) | (value.G << 8) | value.B;
            Apply(() => _service.SetAccentColor(colorInt),
                  $"Accent color set to #{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }

    // Preset accent colors (Windows 11 defaults)
    public static readonly Color[] AccentPresets =
    [
        Color.FromRgb(0, 120, 212),   // Windows Blue
        Color.FromRgb(0, 153, 188),   // Teal
        Color.FromRgb(0, 178, 148),   // Seafoam
        Color.FromRgb(107, 175, 83),  // Sage Green
        Color.FromRgb(218, 59, 1),    // Brick Red
        Color.FromRgb(195, 0, 82),    // Mod Red
        Color.FromRgb(143, 0, 188),   // Purple
        Color.FromRgb(136, 23, 152),  // Orchid
        Color.FromRgb(0, 99, 177),    // Navy Blue
        Color.FromRgb(2, 131, 104),   // Forest
        Color.FromRgb(232, 17, 35),   // Red
        Color.FromRgb(255, 140, 0),   // Gold
    ];

    private async void Apply(
        Func<System.Threading.Tasks.Task<Win11Tweaker.Core.Backup.ApplyResult>> action,
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
