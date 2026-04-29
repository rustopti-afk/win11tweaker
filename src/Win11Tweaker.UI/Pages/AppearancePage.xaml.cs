using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win11Tweaker.UI.ViewModels;

namespace Win11Tweaker.UI.Pages;

public partial class AppearancePage : Page
{
    private AppearanceViewModel _vm = null!;

    public AppearancePage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AppearanceViewModel>();
        DataContext = _vm;

        // Populate color presets
        ColorPresets.ItemsSource = AppearanceViewModel.AccentPresets;
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Color color)
            _vm.AccentColor = color;
    }
}
