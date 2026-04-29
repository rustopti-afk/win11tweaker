using Win11Tweaker.Core.Services;

// Runs elevated (requireAdministrator in app.manifest).
// Accepts commands from the main process via Named Pipe.

var pipeName = ElevatedHelperClient.PipeName;

// Allow custom pipe name via --pipe argument (for testing)
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--pipe") pipeName = args[i + 1];

using var cts = new CancellationTokenSource();

// Graceful shutdown on Ctrl+C
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"[Win11TweakerHelper] Running as: {Environment.UserName}");
Console.WriteLine($"[Win11TweakerHelper] Listening on pipe: {pipeName}");

var server = new ElevatedHelperServer(pipeName);
await server.RunAsync(cts.Token);
