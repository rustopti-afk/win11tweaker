using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.Pages;

// View model for one row in the pack list
internal record InstalledPackItem(string Name, string Dir, string? PreviewPath);

public partial class IconsPage : Page
{
    private readonly IconPackService _svc;
    private string? _selectedFolderPath;
    private string? _selectedIconPath;

    public IconsPage()
    {
        InitializeComponent();
        _svc = App.Services.GetRequiredService<IconPackService>();
        RefreshPacksList();
    }

    // ── Pack list ─────────────────────────────────────────────────────────────

    private void RefreshPacksList()
    {
        var packs = _svc.GetInstalledPacks()
            .Select(p => new InstalledPackItem(p.Name, p.Dir, p.PreviewPath))
            .ToList();

        PacksList.ItemsSource  = packs;
        NoPacksText.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Browse & install pack ─────────────────────────────────────────────────

    private void BrowseIconPack_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Open Icon Pack",
            Filter = "Icon Pack ZIP|*.zip|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
            LoadAndInstallPack(dlg.FileName);
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────────

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("SystemControlForegroundBaseMediumBrush");
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("SystemControlForegroundBaseMediumBrush");
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var zip   = files.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (zip != null)
            LoadAndInstallPack(zip);
        e.Handled = true;
    }

    private void LoadAndInstallPack(string zipPath)
    {
        try
        {
            var manifest = _svc.LoadPack(zipPath);
            SetMainStatus($"✓ Icon pack '{manifest.Name}' installed. Click Apply to use it.");
            RefreshPacksList();
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ Failed to load icon pack: {ex.Message}");
        }
    }

    // ── Apply pack ────────────────────────────────────────────────────────────

    private void ApplyPack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not InstalledPackItem item) return;

        try
        {
            var manifestPath = Path.Combine(item.Dir, "manifest.json");
            var json         = File.ReadAllText(manifestPath);
            var manifest     = System.Text.Json.JsonSerializer.Deserialize<IconPackManifest>(json)!;

            var count = _svc.ApplyPack(manifest, item.Dir);
            SetMainStatus($"✓ Applied '{item.Name}': {count} icon(s) changed. Restart Explorer to see changes.");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ Failed to apply pack: {ex.Message}");
        }
    }

    // ── Custom folder icon ────────────────────────────────────────────────────

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose folder to apply custom icon"
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _selectedFolderPath     = dlg.SelectedPath;
            FolderStatusText.Text   = $"Folder: {_selectedFolderPath}";
            TryEnableApplyFolderBtn();
        }
    }

    private void ChooseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose Icon File",
            Filter = "Icon Files|*.ico|PNG Images|*.png|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _selectedIconPath     = dlg.FileName;
            FolderStatusText.Text = $"Folder: {_selectedFolderPath ?? "(none)"}  |  Icon: {Path.GetFileName(_selectedIconPath)}";
            TryEnableApplyFolderBtn();
        }
    }

    private void TryEnableApplyFolderBtn() =>
        ApplyFolderBtn.IsEnabled = _selectedFolderPath != null && _selectedIconPath != null;

    private void ApplyFolderIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolderPath == null || _selectedIconPath == null) return;
        try
        {
            IconPackService.ApplyFolderIcon(_selectedFolderPath, _selectedIconPath);
            SetMainStatus($"✓ Icon applied to {Path.GetFileName(_selectedFolderPath)}");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ {ex.Message}");
        }
    }

    private void SetMainStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}
