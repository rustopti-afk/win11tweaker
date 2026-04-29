using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Win11Tweaker.Core;
using Win11Tweaker.UI.ViewModels;

namespace Win11Tweaker.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection()
            .AddWin11TweakerCore()
            .AddSingleton<MainViewModel>()
            .AddSingleton<TaskbarViewModel>()
            .AddSingleton<ExplorerViewModel>()
            .AddSingleton<AppearanceViewModel>()
            .BuildServiceProvider();

        Services = services;

        // Apply saved dark/light mode preference
        ModernWpf.ThemeManager.Current.ApplicationTheme =
            ModernWpf.ApplicationTheme.Dark;
    }
}
