using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Win11Tweaker.Core.Profile;

namespace Win11Tweaker.UI.Pages;

// View model for one profile card
internal class ProfileCardItem
{
    public string   Id                  { get; set; } = "";
    public string   Name                { get; set; } = "";
    public string   Description         { get; set; } = "";
    public string   CreatedAtFormatted  { get; set; } = "";
    public string?  PreviewPath         { get; set; }
}

public partial class ProfilesPage : Page
{
    private readonly ProfileManager _manager;

    public ProfilesPage()
    {
        InitializeComponent();
        _manager = App.Services.GetRequiredService<ProfileManager>();
        RefreshList();
    }

    // ── List ──────────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        var profiles = _manager.GetAllProfiles();
        var items    = profiles.Select(p => new ProfileCardItem
        {
            Id                 = p.Id,
            Name               = p.Name,
            Description        = p.Description,
            CreatedAtFormatted = p.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm"),
            PreviewPath        = GetPreviewPath(p.Id)
        }).ToList();

        ProfilesList.ItemsSource  = items;
        NoProfilesText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string? GetPreviewPath(string id)
    {
        var appData  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker", "profiles", id, "preview.png");
        return File.Exists(appData) ? appData : null;
    }

    // ── Save current state ────────────────────────────────────────────────────

    private async void SaveCurrentState_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveProfileDialog();
        if (dlg.ShowDialog() != true) return;

        var profile      = ProfileManager.SnapshotCurrent(dlg.ProfileName);
        profile.Description = dlg.ProfileDescription;

        // Take a desktop screenshot
        var screenshotPath = await CaptureScreenshot(profile.Id);

        await _manager.SaveProfile(profile);
        await _manager.SetActiveProfile(profile.Id);

        RefreshList();
        SetStatus($"✓ Profile saved: {profile.Name}");
    }

    // ── Export ────────────────────────────────────────────────────────────────

    private async void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var profile = await _manager.LoadProfile(id);
        if (profile == null) return;

        var dlg = new SaveFileDialog
        {
            Title      = "Export Profile",
            Filter     = "Win11 Theme|*.w11theme",
            FileName   = $"{profile.Name}.w11theme"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var previewPath = GetPreviewPath(id);
            await _manager.Export(profile, dlg.FileName, previewPath);
            SetStatus($"✓ Exported: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Export failed: {ex.Message}");
        }
    }

    // ── Import ────────────────────────────────────────────────────────────────

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Import .w11theme Profile",
            Filter = "Win11 Theme|*.w11theme|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var profile = await _manager.Import(dlg.FileName);
            RefreshList();
            SetStatus($"✓ Imported: {profile.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Import failed: {ex.Message}");
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var profile = await _manager.LoadProfile(id);
        if (profile == null) return;

        var result = MessageBox.Show(
            $"Apply profile '{profile.Name}'?\n\nThis will change taskbar, explorer, appearance, and wallpaper settings.",
            "Apply Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await ApplyProfileToSystem(profile);
        await _manager.SetActiveProfile(id);
        SetStatus($"✓ Profile applied: {profile.Name}");

        if (Application.Current.MainWindow is MainWindow main)
            main.UpdateChangesCount();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var result = MessageBox.Show("Delete this profile?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _manager.DeleteProfile(id);
        RefreshList();
        SetStatus("Profile deleted.");
    }

    // ── Apply profile to system ───────────────────────────────────────────────

    private async Task ApplyProfileToSystem(StyleProfile profile)
    {
        // Get services
        var taskbarSvc  = App.Services.GetRequiredService<Win11Tweaker.Core.Services.TaskbarTweakService>();
        var explorerSvc = App.Services.GetRequiredService<Win11Tweaker.Core.Services.ExplorerTweakService>();
        var wallpaperSvc = App.Services.GetRequiredService<Win11Tweaker.Core.Services.WallpaperService>();

        // Apply taskbar settings
        await taskbarSvc.SetSearchButtonVisible(profile.Taskbar.ShowSearch);
        await taskbarSvc.SetWidgetsButtonVisible(profile.Taskbar.ShowWidgets);
        await taskbarSvc.SetChatButtonVisible(profile.Taskbar.ShowChat);
        await taskbarSvc.SetTaskbarAlignment(
            profile.Taskbar.CenterAligned
                ? Win11Tweaker.Core.Services.TaskbarAlignment.Center
                : Win11Tweaker.Core.Services.TaskbarAlignment.Left);
        await taskbarSvc.SetDarkMode(profile.Taskbar.DarkMode);
        await taskbarSvc.SetTransparencyEnabled(profile.Taskbar.Transparency);

        // Apply explorer settings
        await explorerSvc.SetOldContextMenu(profile.Explorer.OldContextMenu);
        await explorerSvc.SetShowFileExtensions(profile.Explorer.ShowExtensions);
        await explorerSvc.SetShowHiddenFiles(profile.Explorer.ShowHiddenFiles);
        await explorerSvc.SetCompactMode(profile.Explorer.CompactMode);

        // Apply wallpaper
        if (profile.Wallpaper.LocalPath != null && File.Exists(profile.Wallpaper.LocalPath))
        {
            var style = Enum.TryParse<Win11Tweaker.Core.Services.WallpaperStyle>(
                profile.Wallpaper.Style, out var s) ? s : Win11Tweaker.Core.Services.WallpaperStyle.Fill;
            wallpaperSvc.SetWallpaper(profile.Wallpaper.LocalPath, style);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> CaptureScreenshot(string profileId)
    {
        try
        {
            var dir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Win11Tweaker", "profiles", profileId);
            Directory.CreateDirectory(dir);

            var path   = Path.Combine(dir, "preview.png");
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using var g   = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }
        catch { return null; }
    }

    private void SetStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}

// ── Simple "save profile" dialog ─────────────────────────────────────────────

internal class SaveProfileDialog : Window
{
    public string ProfileName        { get; private set; } = "My Profile";
    public string ProfileDescription { get; private set; } = "";

    private readonly System.Windows.Controls.TextBox _nameBox;
    private readonly System.Windows.Controls.TextBox _descBox;

    public SaveProfileDialog()
    {
        Title              = "Save Profile";
        Width              = 380;
        SizeToContent      = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner              = Application.Current.MainWindow;
        ResizeMode         = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(20) };

        panel.Children.Add(new TextBlock
        {
            Text = "Profile Name", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        _nameBox = new System.Windows.Controls.TextBox
        {
            Text = "My Profile", Margin = new Thickness(0, 0, 0, 14)
        };
        panel.Children.Add(_nameBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Description (optional)", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        _descBox = new System.Windows.Controls.TextBox
        {
            Height = 60, TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true, Margin = new Thickness(0, 0, 0, 16)
        };
        panel.Children.Add(_descBox);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okBtn = new Button
        {
            Content = "Save", Width = 80, Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okBtn.Click += (_, _) =>
        {
            ProfileName        = string.IsNullOrWhiteSpace(_nameBox.Text) ? "My Profile" : _nameBox.Text;
            ProfileDescription = _descBox.Text;
            DialogResult       = true;
        };

        var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        btnRow.Children.Add(okBtn);
        btnRow.Children.Add(cancelBtn);
        panel.Children.Add(btnRow);

        Content = panel;
    }
}
