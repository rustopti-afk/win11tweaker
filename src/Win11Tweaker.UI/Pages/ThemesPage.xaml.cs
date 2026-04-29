using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.Pages;

public partial class ThemesPage : Page
{
    private readonly ThemeService _svc;

    public ThemesPage()
    {
        InitializeComponent();
        _svc = App.Services.GetRequiredService<ThemeService>();
        CheckSutStatus();
        RefreshThemesList();
    }

    private void CheckSutStatus()
    {
        if (_svc.IsSecureUxThemeInstalled())
        {
            SutDot.Fill    = new SolidColorBrush(Color.FromRgb(0, 188, 114));
            SutStatus.Text = "SecureUxTheme installed — custom themes supported";
        }
        else
        {
            SutDot.Fill              = new SolidColorBrush(Color.FromRgb(200, 80, 60));
            SutStatus.Text           = "SecureUxTheme not installed";
            InstallSutBtn.Visibility = Visibility.Visible;
        }
    }

    private void RefreshThemesList()
    {
        ThemesList.ItemsSource = _svc.GetInstalledThemes();
    }

    private async void InstallSut_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Find SecureUxTheme.exe",
            Filter = "Executable|SecureUxTheme.exe|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        InstallSutBtn.IsEnabled = false;
        SutStatus.Text          = "Installing...";

        var ok = await _svc.InstallSecureUxTheme(dlg.FileName);
        CheckSutStatus();
        SetStatus(ok ? "✓ SecureUxTheme installed" : "✗ Installation failed");
    }

    private void InstallTheme_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose Theme File",
            Filter = "Theme files|*.msstyles;*.theme|MsStyles|*.msstyles|Theme|*.theme|All|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext == ".msstyles")
                _svc.InstallCustomTheme(dlg.FileName);

            RefreshThemesList();
            SetStatus($"✓ Theme installed: {Path.GetFileNameWithoutExtension(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ {ex.Message}");
        }
    }

    private async void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            SetStatus("Applying theme...");
            var ok = await _svc.ApplyTheme(path);
            SetStatus(ok ? $"✓ Theme applied: {Path.GetFileNameWithoutExtension(path)}"
                         : "✗ Failed to apply theme");
        }
    }

    private void RefreshThemes_Click(object sender, RoutedEventArgs e) =>
        RefreshThemesList();

    private void SetStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}
