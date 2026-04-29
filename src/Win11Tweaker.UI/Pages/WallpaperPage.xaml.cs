using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.Pages;

public partial class WallpaperPage : Page
{
    private readonly WallpaperService _svc;
    private WallpaperStyle _selectedStyle = WallpaperStyle.Fill;

    public WallpaperPage()
    {
        InitializeComponent();
        _svc = App.Services.GetRequiredService<WallpaperService>();
        CheckLivelyStatus();
    }

    private void CheckLivelyStatus()
    {
        if (_svc.IsLivelyInstalled())
        {
            LivelyStatusDot.Fill  = new SolidColorBrush(Color.FromRgb(0, 188, 114));
            LivelyStatusText.Text = "Lively Wallpaper installed";
            BrowseLivelyBtn.IsEnabled = true;
            StopLivelyBtn.IsEnabled   = true;
        }
        else
        {
            LivelyStatusDot.Fill  = new SolidColorBrush(Color.FromRgb(200, 80, 60));
            LivelyStatusText.Text = "Lively Wallpaper not installed";
            InstallLivelyBtn.Visibility = Visibility.Visible;
        }
    }

    private void BrowseWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose Wallpaper",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _svc.SetWallpaper(dlg.FileName, _selectedStyle);
            PreviewImage.Source = new BitmapImage(new Uri(dlg.FileName));
            SetMainStatus($"✓ Wallpaper set: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ {ex.Message}");
        }
    }

    private void SolidColor_Click(object sender, RoutedEventArgs e)
    {
        // Use Windows color picker dialog via System.Drawing
        var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        try
        {
            _svc.SetSolidColor(dlg.Color);
            WallpaperPreview.Background = new SolidColorBrush(
                Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B));
            PreviewImage.Source = null;
            SetMainStatus($"✓ Background color set to #{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ {ex.Message}");
        }
    }

    private void StyleRadio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (StyleRadio.SelectedItem is RadioButton rb)
        {
            _selectedStyle = rb.Tag?.ToString() switch
            {
                "Fill"    => WallpaperStyle.Fill,
                "Fit"     => WallpaperStyle.Fit,
                "Stretch" => WallpaperStyle.Stretch,
                "Tile"    => WallpaperStyle.Tile,
                "Center"  => WallpaperStyle.Center,
                "Span"    => WallpaperStyle.Span,
                _         => WallpaperStyle.Fill
            };
        }
    }

    private async void InstallLively_Click(object sender, RoutedEventArgs e)
    {
        InstallLivelyBtn.IsEnabled = false;
        LivelyStatusText.Text      = "Installing Lively Wallpaper via winget...";
        SetMainStatus("Installing Lively Wallpaper...");

        var success = await _svc.InstallLively();

        if (success)
        {
            CheckLivelyStatus();
            SetMainStatus("✓ Lively Wallpaper installed");
        }
        else
        {
            LivelyStatusText.Text      = "Installation failed — install manually from lively.rocks";
            InstallLivelyBtn.IsEnabled = true;
            SetMainStatus("✗ Lively install failed");
        }
    }

    private async void BrowseLively_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose Animated Wallpaper",
            Filter = "Videos & Web|*.mp4;*.webm;*.gif;*.html;*.htm|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        SetMainStatus("Setting animated wallpaper...");
        var ok = await _svc.SetLivelyWallpaper(dlg.FileName);
        SetMainStatus(ok
            ? $"✓ Animated wallpaper: {Path.GetFileName(dlg.FileName)}"
            : "✗ Lively Wallpaper failed to set the wallpaper");
    }

    private async void StopLively_Click(object sender, RoutedEventArgs e)
    {
        await _svc.StopLively();
        SetMainStatus("✓ Animated wallpaper stopped");
    }

    private void SetMainStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}
