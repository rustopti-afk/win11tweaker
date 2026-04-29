#if WINDOWS
using Microsoft.Extensions.DependencyInjection;
using Win11Tweaker.Core.Backup;
using Win11Tweaker.Core.Profile;
using Win11Tweaker.Core.Registry;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.Core;

public static class ServiceRegistration
{
    /// <summary>
    /// Register all Win11Tweaker.Core services into the DI container.
    /// Call this from the UI project's startup.
    /// </summary>
    public static IServiceCollection AddWin11TweakerCore(this IServiceCollection services)
    {
        services.AddSingleton<RegistryService>();
        services.AddSingleton<ChangeTracker>();
        services.AddSingleton<TaskbarTweakService>();
        services.AddSingleton<ExplorerTweakService>();
        services.AddSingleton<ElevatedHelperClient>();
        services.AddSingleton<TaskbarBlurService>();
        services.AddSingleton<IconPackService>();
        services.AddSingleton<WallpaperService>();
        services.AddSingleton<FontService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<RainmeterService>();
        services.AddSingleton<WindhawkService>();
        services.AddSingleton<ProfileManager>();
        return services;
    }
}
#endif
