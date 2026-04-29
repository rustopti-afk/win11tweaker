using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.Pages;

public partial class ToolsPage : Page
{
    private readonly RainmeterService _rm;
    private readonly WindhawkService  _wh;

    public ToolsPage()
    {
        InitializeComponent();

        // Register converters needed by XAML
        Resources["BoolToColorConverter"]       = new Converters.BoolToColorConverter();
        Resources["BoolToInstallTextConverter"] = new Converters.BoolToInstallTextConverter();

        _rm = App.Services.GetRequiredService<RainmeterService>();
        _wh = App.Services.GetRequiredService<WindhawkService>();

        CheckRainmeterStatus();
        CheckWindhawkStatus();
        _ = LoadModsAsync();
    }

    // ── Rainmeter ─────────────────────────────────────────────────────────────

    private void CheckRainmeterStatus()
    {
        if (_rm.IsInstalled())
        {
            RmDot.Fill    = new SolidColorBrush(Color.FromRgb(0, 188, 114));
            RmStatus.Text = "Rainmeter installed";
            LaunchRmBtn.IsEnabled = true;
            RefreshSkins();
        }
        else
        {
            RmDot.Fill              = new SolidColorBrush(Color.FromRgb(200, 80, 60));
            RmStatus.Text           = "Rainmeter not installed";
            InstallRmBtn.Visibility = Visibility.Visible;
        }
    }

    private void RefreshSkins() =>
        SkinsList.ItemsSource = _rm.GetInstalledSkins();

    private async void InstallRainmeter_Click(object sender, RoutedEventArgs e)
    {
        InstallRmBtn.IsEnabled = false;
        RmStatus.Text          = "Installing Rainmeter via winget...";
        var ok = await _rm.Install();
        CheckRainmeterStatus();
        SetStatus(ok ? "✓ Rainmeter installed" : "✗ Install failed — try manually from rainmeter.net");
    }

    private void LaunchRainmeter_Click(object sender, RoutedEventArgs e) => _rm.Launch();

    private void InstallSkin_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Install Rainmeter Skin",
            Filter = "Rainmeter Skin|*.rmskin;*.zip|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _rm.InstallSkin(dlg.FileName);
            RefreshSkins();
            SetStatus($"✓ Skin installed: {Path.GetFileNameWithoutExtension(dlg.FileName)}");
        }
        catch (Exception ex) { SetStatus($"✗ {ex.Message}"); }
    }

    private async void ActivateSkin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RainmeterSkin skin)
        {
            await _rm.ActivateSkin(skin.ConfigName, Path.GetFileName(skin.Path));
            SetStatus($"✓ Activated: {skin.DisplayName}");
        }
    }

    private async void DeactivateSkin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string configName)
        {
            await _rm.DeactivateSkin(configName);
            SetStatus($"✓ Deactivated: {configName}");
        }
    }

    // ── Windhawk ──────────────────────────────────────────────────────────────

    private void CheckWindhawkStatus()
    {
        if (_wh.IsInstalled())
        {
            WhDot.Fill    = new SolidColorBrush(Color.FromRgb(0, 188, 114));
            WhStatus.Text = "Windhawk installed";
            LaunchWhBtn.IsEnabled = true;
        }
        else
        {
            WhDot.Fill              = new SolidColorBrush(Color.FromRgb(200, 80, 60));
            WhStatus.Text           = "Windhawk not installed";
            InstallWhBtn.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadModsAsync()
    {
        var mods = await _wh.GetRecommendedModsWithStatus();
        ModsList.ItemsSource = mods;
    }

    private async void InstallWindhawk_Click(object sender, RoutedEventArgs e)
    {
        InstallWhBtn.IsEnabled = false;
        WhStatus.Text          = "Installing Windhawk via winget...";
        var ok = await _wh.Install();
        CheckWindhawkStatus();
        SetStatus(ok ? "✓ Windhawk installed" : "✗ Install failed — try manually from windhawk.net");
    }

    private void LaunchWindhawk_Click(object sender, RoutedEventArgs e) => _wh.Launch();

    private async void RefreshMods_Click(object sender, RoutedEventArgs e) =>
        await LoadModsAsync();

    private async void ToggleMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not WindhawkMod mod) return;

        btn.IsEnabled = false;
        if (mod.IsInstalled)
        {
            var ok = await _wh.UninstallMod(mod.Id);
            SetStatus(ok ? $"✓ Mod removed: {mod.Name}" : $"✗ Failed to remove {mod.Name}");
        }
        else
        {
            var ok = await _wh.InstallMod(mod.Id);
            SetStatus(ok ? $"✓ Mod installed: {mod.Name}" : $"✗ Failed to install {mod.Name}");
        }

        await LoadModsAsync();
        btn.IsEnabled = true;
    }

    private void OpenModPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
            _wh.OpenModPage(id);
    }

    private void SetStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}
