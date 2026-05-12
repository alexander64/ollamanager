using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OllamaManager.Services;

public class ManagedProcess : IDisposable
{
    // ── Global registry so exit handlers can kill everything ──────────────
    private static readonly object _registryLock = new();
    private static readonly List<ManagedProcess> _registry = new();

    internal static void KillAll()
    {
        List<ManagedProcess> snapshot;
        lock (_registryLock) snapshot = [.._registry];
        foreach (var mp in snapshot) mp.Stop();
    }
    // ─────────────────────────────────────────────────────────────────────

    private Process? _process;

    public event Action<string>? OutputReceived;
    public event Action? Exited;

    public bool IsRunning => _process is { HasExited: false };
    public int? ProcessId => _process?.Id;

    public ManagedProcess()
    {
        lock (_registryLock) _registry.Add(this);
    }

    public void Start(string command, string arguments, Dictionary<string, string>? env = null, string? workingDirectory = null)
    {
        Stop();

        var psi = new ProcessStartInfo(ResolveCommand(command), arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = workingDirectory ?? string.Empty,
        };

        psi.Environment["PATH"] = BuildPath();

        if (env != null)
            foreach (var (k, v) in env)
                psi.Environment[k] = v;

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data); };
        proc.Exited             += (_, _) => Exited?.Invoke();

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            _process = proc;
        }
        catch
        {
            proc.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attaches to an already-running process by PID.
    /// Monitors exit; output cannot be captured from an existing process.
    /// </summary>
    public bool Adopt(int pid)
    {
        Stop();
        try
        {
            var p = Process.GetProcessById(pid);
            if (p.HasExited) return false;
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => Exited?.Invoke();
            _process = p;
            return true;
        }
        catch { return false; }
    }

    public void Stop()
    {
        var proc = _process;
        _process = null;
        if (proc == null) return;
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(3000);
            }
        }
        catch { }
        finally { proc.Dispose(); }
    }

    public void Dispose()
    {
        Stop();
        lock (_registryLock) _registry.Remove(this);
    }

    /// <summary>
    /// Returns PIDs of all processes currently listening on the given TCP port.
    /// </summary>
    public static int[] FindPidsByPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo(ResolveCommand("lsof"), $"-i :{port} -t -sTCP:LISTEN")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            psi.Environment["PATH"] = BuildPath();
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
        }
        catch { return []; }
    }

    /// <summary>
    /// Returns the bin/ directory of the Python runtime bundled inside the .app,
    /// or null when running outside the bundle (e.g. during development).
    /// Path: .app/Contents/MacOS/OllamaManager → .app/Contents/Resources/python/bin
    /// </summary>
    public static string? FindBundledPythonBin()
    {
        try
        {
            var exe = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exe)) return null;
            var macosDir    = System.IO.Path.GetDirectoryName(exe) ?? "";
            var contentsDir = System.IO.Path.GetDirectoryName(macosDir) ?? "";
            var pythonBin   = System.IO.Path.Combine(contentsDir, "Resources", "python", "bin");
            return System.IO.Directory.Exists(pythonBin) ? pythonBin : null;
        }
        catch { return null; }
    }

    public static string BuildPath()
    {
        var home       = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pyenvShims = System.IO.Path.Combine(home, ".pyenv", "shims");
        var pyenvBin   = System.IO.Path.Combine(home, ".pyenv", "bin");
        var systemPath = $"{pyenvShims}:{pyenvBin}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";

        // Il Python bundled ha la precedenza — copre mlx_lm.server, hf, pip3, etc.
        var bundledBin = FindBundledPythonBin();
        return bundledBin != null ? $"{bundledBin}:{systemPath}" : systemPath;
    }

    // Resolves a bare command name to its absolute path by searching BuildPath() directories.
    // Returns the original name unchanged if not found (let the OS error surface naturally).
    public static string ResolveCommand(string command)
    {
        if (System.IO.Path.IsPathRooted(command)) return command;
        foreach (var dir in BuildPath().Split(':', System.StringSplitOptions.RemoveEmptyEntries))
        {
            var full = System.IO.Path.Combine(dir, command);
            if (System.IO.File.Exists(full)) return full;
        }
        return command;
    }
}
