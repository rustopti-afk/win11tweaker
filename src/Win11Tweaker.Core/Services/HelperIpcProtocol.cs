#if WINDOWS
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Win11Tweaker.Core.Services;

// ── Messages ──────────────────────────────────────────────────────────────────

public record HelperRequest(
    string Command,
    Dictionary<string, string> Args
);

public record HelperResponse(
    bool Success,
    string? Error = null,
    string? Result = null
);

// ── Commands the Helper understands ──────────────────────────────────────────

public static class HelperCommands
{
    public const string Ping             = "ping";
    public const string SetRegistryHklm  = "set_registry_hklm";
    public const string DeleteRegistryHklm = "delete_registry_hklm";
    public const string CopyFile         = "copy_file";
    public const string TakeOwnership    = "take_ownership";
    public const string RunSetup         = "run_setup";
    public const string RegisterWatchdog = "register_watchdog";
    public const string UnregisterWatchdog = "unregister_watchdog";
}

// ── Client (runs in main process) ────────────────────────────────────────────

/// <summary>
/// Sends commands to Win11TweakerHelper.exe over a named pipe.
/// The helper runs elevated (admin) and performs operations that require UAC.
/// </summary>
public class ElevatedHelperClient : IDisposable
{
    public const string PipeName = "Win11TweakerHelper_v1";
    private NamedPipeClientStream? _pipe;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool IsHelperRunning() =>
        System.Diagnostics.Process.GetProcessesByName("Win11TweakerHelper").Length > 0;

    /// <summary>
    /// Ensure helper is running. Returns false if user cancelled the UAC prompt.
    /// </summary>
    public static async Task<bool> EnsureRunning()
    {
        if (IsHelperRunning()) return true;

        var helperPath = Path.Combine(AppContext.BaseDirectory, "Win11TweakerHelper.exe");
        if (!File.Exists(helperPath)) return false;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(helperPath)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"--pipe {PipeName}"
            };
            System.Diagnostics.Process.Start(psi);

            // Wait up to 5 seconds for helper to open the pipe
            using var cts = new CancellationTokenSource(5000);
            while (!cts.IsCancellationRequested)
            {
                if (IsHelperRunning()) return true;
                await Task.Delay(150, cts.Token);
            }
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // User clicked "No" on UAC
        }
    }

    public async Task<HelperResponse> Send(HelperRequest request)
    {
        await EnsureConnected();

        var json = JsonSerializer.Serialize(request, JsonOpts);
        await WriteMessage(_pipe!, json);
        var responseJson = await ReadMessage(_pipe!);
        return JsonSerializer.Deserialize<HelperResponse>(responseJson, JsonOpts)
               ?? new HelperResponse(false, "Empty response");
    }

    private async Task EnsureConnected()
    {
        if (_pipe?.IsConnected == true) return;

        _pipe?.Dispose();
        _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(timeout: 3000);
    }

    private static async Task WriteMessage(PipeStream pipe, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var lengthBytes = BitConverter.GetBytes(bytes.Length);
        await pipe.WriteAsync(lengthBytes);
        await pipe.WriteAsync(bytes);
        await pipe.FlushAsync();
    }

    private static async Task<string> ReadMessage(PipeStream pipe)
    {
        var lengthBytes = new byte[4];
        await pipe.ReadExactlyAsync(lengthBytes);
        var length = BitConverter.ToInt32(lengthBytes);

        var buffer = new byte[length];
        await pipe.ReadExactlyAsync(buffer);
        return Encoding.UTF8.GetString(buffer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _pipe?.Dispose();
        _disposed = true;
    }
}

// ── Server (runs in Helper process) ──────────────────────────────────────────

/// <summary>
/// Named pipe server that runs inside Win11TweakerHelper.exe (elevated).
/// Listens for commands from the main process and executes them with admin rights.
/// </summary>
public class ElevatedHelperServer
{
    private readonly string _pipeName;
    private bool _running;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ElevatedHelperServer(string pipeName = ElevatedHelperClient.PipeName)
    {
        _pipeName = pipeName;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _running = true;
        while (_running && !ct.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                _pipeName, PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.Asynchronous);

            await pipe.WaitForConnectionAsync(ct);

            _ = Task.Run(async () =>
            {
                try { await HandleConnection(pipe, ct); }
                catch { /* client disconnected */ }
            }, ct);
        }
    }

    private async Task HandleConnection(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            var json = await ReadMessage(pipe);
            var request = JsonSerializer.Deserialize<HelperRequest>(json, JsonOpts)!;
            var response = await ExecuteCommand(request);
            await WriteMessage(pipe, JsonSerializer.Serialize(response, JsonOpts));
        }
    }

    private static async Task<HelperResponse> ExecuteCommand(HelperRequest req)
    {
        try
        {
            return req.Command switch
            {
                HelperCommands.Ping => new HelperResponse(true, Result: "pong"),

                HelperCommands.CopyFile => CopyFile(req.Args["src"], req.Args["dest"]),

                HelperCommands.RunSetup => await RunSetup(
                    req.Args["path"],
                    req.Args.GetValueOrDefault("args", "")),

                _ => new HelperResponse(false, $"Unknown command: {req.Command}")
            };
        }
        catch (Exception ex)
        {
            return new HelperResponse(false, ex.Message);
        }
    }

    private static HelperResponse CopyFile(string src, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(src, dest, overwrite: true);
        return new HelperResponse(true);
    }

    private static async Task<HelperResponse> RunSetup(string path, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(path, args)
        {
            UseShellExecute = true
        };
        var process = System.Diagnostics.Process.Start(psi)!;
        await process.WaitForExitAsync();
        return process.ExitCode == 0
            ? new HelperResponse(true)
            : new HelperResponse(false, $"Exit code: {process.ExitCode}");
    }

    private static async Task WriteMessage(PipeStream pipe, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length));
        await pipe.WriteAsync(bytes);
        await pipe.FlushAsync();
    }

    private static async Task<string> ReadMessage(PipeStream pipe)
    {
        var lengthBytes = new byte[4];
        await pipe.ReadExactlyAsync(lengthBytes);
        var buffer = new byte[BitConverter.ToInt32(lengthBytes)];
        await pipe.ReadExactlyAsync(buffer);
        return Encoding.UTF8.GetString(buffer);
    }
}
#endif
