using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Win11Tweaker.Core.Backup;
using Win11Tweaker.UI.Pages;

namespace Win11Tweaker.UI;

public partial class MainWindow : Window
{
    private readonly ChangeTracker _changeTracker;
    private Button? _activeNavButton;

    public MainWindow()
    {
        InitializeComponent();
        _changeTracker = App.Services.GetRequiredService<ChangeTracker>();

        // Start on Taskbar page
        Navigate("Taskbar");
        SetActiveButton(BtnTaskbar);
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            Navigate(btn.Tag?.ToString() ?? "Taskbar");
            SetActiveButton(btn);
        }
    }

    private void Navigate(string page)
    {
        Page? content = page switch
        {
            "Taskbar"    => new TaskbarPage(),
            "Explorer"   => new ExplorerPage(),
            "Appearance" => new AppearancePage(),
            "Icons"      => new IconsPage(),
            "Fonts"      => new FontsPage(),
            "Wallpaper"  => new WallpaperPage(),
            "Themes"     => new ThemesPage(),
            "Tools"      => new ToolsPage(),
            "Profiles"   => new ProfilesPage(),
            "Settings"   => new SettingsPage(),
            "About"      => new AboutPage(),
            _            => new TaskbarPage()
        };
        ContentFrame.Navigate(content);
        StatusText.Text = $"{page} settings";
    }

    private void SetActiveButton(Button active)
    {
        // Reset previous
        if (_activeNavButton != null)
            _activeNavButton.Tag = _activeNavButton.Tag; // triggers style refresh via tag

        _activeNavButton = active;
        UpdateChangesCount();
    }

    private async void UndoLast_Click(object sender, RoutedEventArgs e)
    {
        var rolled = await _changeTracker.RollbackLast();
        if (rolled)
        {
            SetStatus("Last change undone.", success: true);
        }
        else
        {
            SetStatus("Nothing to undo.", success: false);
        }
        UpdateChangesCount();
    }

    private async void UndoAll_Click(object sender, RoutedEventArgs e)
    {
        var count = _changeTracker.History.Count;
        if (count == 0)
        {
            SetStatus("No changes to undo.", success: false);
            return;
        }

        var result = MessageBox.Show(
            $"This will undo all {count} change(s) and restore your original settings.\n\nContinue?",
            "Undo All Changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _changeTracker.RollbackAll();
            SetStatus($"All {count} changes undone.", success: true);
            UpdateChangesCount();
        }
    }

    public void SetStatus(string message, bool success = true)
    {
        StatusText.Text = message;
        StatusText.Foreground = success
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 188, 114))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 100, 80));

        // Reset to neutral after 3 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (_, _) =>
        {
            StatusText.Text = "Ready";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource(
                "SystemControlForegroundBaseMediumBrush");
            timer.Stop();
        };
        timer.Start();
    }

    public void UpdateChangesCount()
    {
        var count = _changeTracker.History.Count;
        ChangesCount.Text = count == 0 ? "No changes" : $"{count} change(s)";
    }
}
