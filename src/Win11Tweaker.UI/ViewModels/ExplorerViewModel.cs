using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.ViewModels;

public class ExplorerViewModel : BaseViewModel
{
    private readonly ExplorerTweakService _service;

    public ExplorerViewModel(ExplorerTweakService service)
    {
        _service = service;
    }

    private bool _oldContextMenu;
    public bool OldContextMenu
    {
        get => _oldContextMenu;
        set
        {
            if (!Set(ref _oldContextMenu, value)) return;
            Apply(() => _service.SetOldContextMenu(value),
                  value ? "Old context menu restored" : "New context menu restored");
        }
    }

    private bool _showExtensions;
    public bool ShowExtensions
    {
        get => _showExtensions;
        set
        {
            if (!Set(ref _showExtensions, value)) return;
            Apply(() => _service.SetShowFileExtensions(value),
                  value ? "File extensions visible" : "File extensions hidden");
        }
    }

    private bool _showHiddenFiles;
    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        set
        {
            if (!Set(ref _showHiddenFiles, value)) return;
            Apply(() => _service.SetShowHiddenFiles(value),
                  value ? "Hidden files visible" : "Hidden files hidden");
        }
    }

    private bool _compactMode;
    public bool CompactMode
    {
        get => _compactMode;
        set
        {
            if (!Set(ref _compactMode, value)) return;
            Apply(() => _service.SetCompactMode(value),
                  value ? "Compact mode on" : "Compact mode off");
        }
    }

    private bool _launchToThisPC = true;
    public bool LaunchToThisPC
    {
        get => _launchToThisPC;
        set
        {
            if (!Set(ref _launchToThisPC, value)) return;
            Apply(() => _service.SetLaunchToThisPC(value),
                  value ? "Explorer opens to This PC" : "Explorer opens to Quick Access");
        }
    }

    private bool _desktopIconsVisible = true;
    public bool DesktopIconsVisible
    {
        get => _desktopIconsVisible;
        set
        {
            if (!Set(ref _desktopIconsVisible, value)) return;
            Apply(() => _service.SetDesktopIconsVisible(value),
                  value ? "Desktop icons shown" : "Desktop icons hidden");
        }
    }

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
