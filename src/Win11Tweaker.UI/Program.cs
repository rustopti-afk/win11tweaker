using Microsoft.Extensions.DependencyInjection;
using Win11Tweaker.Core;
using Win11Tweaker.Core.Services;

// Bootstrap DI
var services = new ServiceCollection()
    .AddWin11TweakerCore()
    .BuildServiceProvider();

// TODO: Replace with WinUI 3 / WinForms application entry point.
// For now this is a CLI placeholder that confirms the core logic wires up.
Console.WriteLine("Win11 Tweaker — Core wired up successfully.");
Console.WriteLine("Run from Windows to apply tweaks.");

var taskbar = services.GetRequiredService<TaskbarTweakService>();
var explorer = services.GetRequiredService<ExplorerTweakService>();
var tracker = services.GetRequiredService<Win11Tweaker.Core.Backup.ChangeTracker>();

Console.WriteLine($"Services ready: TaskbarTweakService, ExplorerTweakService, ChangeTracker");
Console.WriteLine("UI shell coming in the next phase (WinForms → WinUI 3 migration).");
