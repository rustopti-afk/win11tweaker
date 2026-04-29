using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Win11Tweaker.UI.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage() => InitializeComponent();

    private void ThemeRadio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeRadio.SelectedItem is RadioButton rb)
        {
            ModernWpf.ThemeManager.Current.ApplicationTheme = rb.Tag?.ToString() switch
            {
                "Dark"    => ModernWpf.ApplicationTheme.Dark,
                "Light"   => ModernWpf.ApplicationTheme.Light,
                _         => null  // System default
            };
        }
    }

    private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker", "backups");
        Directory.CreateDirectory(backupDir);
        Process.Start("explorer.exe", backupDir);
    }
}
