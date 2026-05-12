using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using OllamaManager.Services;
using ReactiveUI;

namespace OllamaManager;

class Program
{
    private static IDisposable? _sigterm;
    private static IDisposable? _sigint;

    static readonly string LogPath = "/tmp/OllamaManager.log";

    static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {msg}\n"); } catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        Log("--- app start ---");

        RxApp.DefaultExceptionHandler = System.Reactive.Observer.Create<Exception>(ex =>
        {
            Log($"RxApp unhandled: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
        });

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log($"UnhandledException: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
            try { ManagedProcess.KillAll(); } catch { }
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => ManagedProcess.KillAll();

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => ManagedProcess.KillAll());
            _sigint  = PosixSignalRegistration.Create(PosixSignal.SIGINT,  _ => ManagedProcess.KillAll());
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        catch (Exception ex)
        {
            Log($"StartWithClassicDesktopLifetime threw: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
            ManagedProcess.KillAll();
        }

        Log("--- app exit ---");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
