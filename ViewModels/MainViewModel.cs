using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using OllamaManager.Models;
using OllamaManager.Services;
using ReactiveUI;

namespace OllamaManager.ViewModels;

public class MainViewModel : ReactiveObject, IDisposable
{
    private readonly DatabaseService _db = new();
    private readonly ManagedProcess _ollama = new();
    private readonly ManagedProcess _mlx = new();
    private readonly ManagedProcess _openWebUI = new();
    private readonly Subject<Unit> _envChanged = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly HuggingFaceService _hf = new();
    private ModelsViewModel? _modelsVm;

    public ModelsViewModel Models => _modelsVm ??= new ModelsViewModel(this, _hf);

    private record EnvKv(string Key, string Value);

    // ── Navigation ────────────────────────────────────────────────────────────
    private int _selectedPage;
    public int SelectedPage
    {
        get => _selectedPage;
        set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
    }

    // ── HuggingFace ───────────────────────────────────────────────────────────
    private string _hfToken;
    public string HfToken
    {
        get => _hfToken;
        set => this.RaiseAndSetIfChanged(ref _hfToken, value);
    }

    // ── Ollama ────────────────────────────────────────────────────────────────
    private bool _ollamaRunning;
    public bool OllamaRunning
    {
        get => _ollamaRunning;
        private set => this.RaiseAndSetIfChanged(ref _ollamaRunning, value);
    }

    private string _ollamaLog = string.Empty;
    public string OllamaLog
    {
        get => _ollamaLog;
        private set => this.RaiseAndSetIfChanged(ref _ollamaLog, value);
    }

    private string _flashAttention;
    public string FlashAttention
    {
        get => _flashAttention;
        set => this.RaiseAndSetIfChanged(ref _flashAttention, value);
    }

    private string _kvCacheType;
    public string KvCacheType
    {
        get => _kvCacheType;
        set => this.RaiseAndSetIfChanged(ref _kvCacheType, value);
    }

    private bool _newEngine;
    public bool NewEngine
    {
        get => _newEngine;
        set => this.RaiseAndSetIfChanged(ref _newEngine, value);
    }

    private string _keepAlive;
    public string KeepAlive
    {
        get => _keepAlive;
        set => this.RaiseAndSetIfChanged(ref _keepAlive, value);
    }

    public ObservableCollection<EnvEntry> OllamaCustomEnv { get; } = new();

    // ── MLX LM ───────────────────────────────────────────────────────────────
    private bool _mlxVlmInstalled;
    public bool MlxVlmInstalled
    {
        get => _mlxVlmInstalled;
        private set => this.RaiseAndSetIfChanged(ref _mlxVlmInstalled, value);
    }

    private string _mlxEngineMode = "Auto";
    public string MlxEngineMode
    {
        get => _mlxEngineMode;
        set => this.RaiseAndSetIfChanged(ref _mlxEngineMode, value);
    }

    public string[] EngineModeOptions { get; } = ["Auto", "LM", "VLM"];

    private string _mlxEngineName = "LM";
    public string MlxEngineName
    {
        get => _mlxEngineName;
        private set => this.RaiseAndSetIfChanged(ref _mlxEngineName, value);
    }

    private ObservableAsPropertyHelper<bool>? _showInstallVlmButton;
    public bool ShowInstallVlmButton => _showInstallVlmButton?.Value ?? false;

    private bool _mlxRunning;
    public bool MlxRunning
    {
        get => _mlxRunning;
        private set => this.RaiseAndSetIfChanged(ref _mlxRunning, value);
    }

    private string _mlxLog = string.Empty;
    public string MlxLog
    {
        get => _mlxLog;
        private set => this.RaiseAndSetIfChanged(ref _mlxLog, value);
    }

    private string _mlxModel;
    public string MlxModel
    {
        get => _mlxModel;
        set => this.RaiseAndSetIfChanged(ref _mlxModel, value);
    }

    private string _mlxDraftModel;
    public string MlxDraftModel
    {
        get => _mlxDraftModel;
        set => this.RaiseAndSetIfChanged(ref _mlxDraftModel, value);
    }

    private string _mlxKvBits;
    public string MlxKvBits
    {
        get => _mlxKvBits;
        set => this.RaiseAndSetIfChanged(ref _mlxKvBits, value);
    }

    public string[] KvBitsOptions { get; } = ["", "4", "8"];

    private string _mlxHost;
    public string MlxHost
    {
        get => _mlxHost;
        set => this.RaiseAndSetIfChanged(ref _mlxHost, value);
    }

    private string _mlxPort;
    public string MlxPort
    {
        get => _mlxPort;
        set => this.RaiseAndSetIfChanged(ref _mlxPort, value);
    }

    public ObservableCollection<EnvEntry> MlxCustomEnv { get; } = new();

    // ── Open WebUI ────────────────────────────────────────────────────────────
    private bool _openWebUIRunning;
    public bool OpenWebUIRunning
    {
        get => _openWebUIRunning;
        private set => this.RaiseAndSetIfChanged(ref _openWebUIRunning, value);
    }

    private bool _openWebUIInstalled;
    public bool OpenWebUIInstalled
    {
        get => _openWebUIInstalled;
        private set => this.RaiseAndSetIfChanged(ref _openWebUIInstalled, value);
    }

    private string _openWebUILog = string.Empty;
    public string OpenWebUILog
    {
        get => _openWebUILog;
        private set => this.RaiseAndSetIfChanged(ref _openWebUILog, value);
    }

    private string _openWebUIPort;
    public string OpenWebUIPort
    {
        get => _openWebUIPort;
        set => this.RaiseAndSetIfChanged(ref _openWebUIPort, value);
    }

    public ObservableCollection<EnvEntry> OpenWebUICustomEnv { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────
    public ReactiveCommand<Unit, Unit> StartOllamaCommand { get; }
    public ReactiveCommand<Unit, Unit> StopOllamaCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearOllamaLogCommand { get; }
    public ReactiveCommand<Unit, Unit> AddOllamaEnvCommand { get; }

    public ReactiveCommand<Unit, Unit> StartMlxCommand { get; }
    public ReactiveCommand<Unit, Unit> StopMlxCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMlxLogCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMlxEnvCommand { get; }
    public ReactiveCommand<Unit, Unit> InstallMlxVlmCommand { get; }

    public ReactiveCommand<Unit, Unit> StartOpenWebUICommand { get; }
    public ReactiveCommand<Unit, Unit> StopOpenWebUICommand { get; }
    public ReactiveCommand<Unit, Unit> InstallOpenWebUICommand { get; }
    public ReactiveCommand<Unit, Unit> OpenBrowserCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearOpenWebUILogCommand { get; }
    public ReactiveCommand<Unit, Unit> AddOpenWebUIEnvCommand { get; }

    public MainViewModel()
    {
        _flashAttention = _db.Get("OLLAMA_FLASH_ATTENTION", "1");
        _kvCacheType    = _db.Get("OLLAMA_KV_CACHE_TYPE", "q8_0");
        _newEngine      = _db.Get("OLLAMA_NEW_ENGINE", "true") == "true";
        _keepAlive      = _db.Get("OLLAMA_KEEP_ALIVE", "1h");
        _mlxModel       = _db.Get("MLX_MODEL", "mlx-community/Llama-3.2-3B-Instruct-4bit");
        _mlxDraftModel  = _db.Get("MLX_DRAFT_MODEL", "");
        _mlxKvBits      = _db.Get("MLX_KV_BITS", "");
        _mlxEngineMode  = _db.Get("MLX_ENGINE_MODE", "Auto");
        _mlxHost        = _db.Get("MLX_HOST", "127.0.0.1");
        _mlxPort        = _db.Get("MLX_PORT", "8081");
        _openWebUIPort  = _db.Get("OPENWEBUI_PORT", "8080");
        _hfToken        = _db.Get("HF_TOKEN", "");

        _hf.SetToken(_hfToken);

        this.WhenAnyValue(x => x.HfToken)
            .Throttle(TimeSpan.FromMilliseconds(800))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(token =>
            {
                _hf.SetToken(token);
                try { _db.Set("HF_TOKEN", token); } catch { }
            })
            .DisposeWith(_subscriptions);

        LoadCustomEnv(_db.Get("OLLAMA_CUSTOM_ENV", "[]"), OllamaCustomEnv);
        LoadCustomEnv(_db.Get("MLX_CUSTOM_ENV", "[]"), MlxCustomEnv);
        LoadCustomEnv(_db.Get("OPENWEBUI_CUSTOM_ENV", "[]"), OpenWebUICustomEnv);

        // update engine badge whenever MlxModel or MlxEngineMode changes
        this.WhenAnyValue(x => x.MlxModel, x => x.MlxEngineMode)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(t =>
            {
                var (model, mode) = t;
                MlxEngineName = mode switch
                {
                    "VLM" => "VLM",
                    "LM"  => "LM",
                    _     => HuggingFaceService.IsVlmModel(model, MlxDataDir) ? "VLM" : "LM"
                };
            })
            .DisposeWith(_subscriptions);

        _showInstallVlmButton = this
            .WhenAnyValue(x => x.MlxVlmInstalled, installed => !installed)
            .ToProperty(this, x => x.ShowInstallVlmButton)
            .DisposeWith(_subscriptions);

        this.WhenAnyValue(x => x.FlashAttention, x => x.KvCacheType, x => x.NewEngine,
                          x => x.KeepAlive, x => x.MlxModel, x => x.MlxDraftModel, x => x.MlxHost)
            .Throttle(TimeSpan.FromMilliseconds(600))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => SaveSettings())
            .DisposeWith(_subscriptions);

        this.WhenAnyValue(x => x.OpenWebUIPort, x => x.MlxKvBits, x => x.MlxEngineMode)
            .Throttle(TimeSpan.FromMilliseconds(600))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => SaveSettings())
            .DisposeWith(_subscriptions);

        _envChanged
            .Throttle(TimeSpan.FromMilliseconds(600))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => SaveSettings())
            .DisposeWith(_subscriptions);

        _ollama.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendOllamaLog(line));
        _ollama.Exited += () => Dispatcher.UIThread.Post(() => OllamaRunning = false);

        _mlx.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendMlxLog(line));
        _mlx.Exited += () => Dispatcher.UIThread.Post(() => MlxRunning = false);

        _openWebUI.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendOpenWebUILog(line));
        _openWebUI.Exited += () => Dispatcher.UIThread.Post(() => OpenWebUIRunning = false);

        StartOllamaCommand = ReactiveCommand.Create(StartOllama,
            this.WhenAnyValue(x => x.OllamaRunning, r => !r));
        StopOllamaCommand = ReactiveCommand.Create(StopOllama,
            this.WhenAnyValue(x => x.OllamaRunning));
        ClearOllamaLogCommand = ReactiveCommand.Create(() => { OllamaLog = string.Empty; });
        AddOllamaEnvCommand = ReactiveCommand.Create(() =>
            OllamaCustomEnv.Add(MakeEntry(OllamaCustomEnv)));

        StartMlxCommand = ReactiveCommand.Create(StartMlx,
            this.WhenAnyValue(x => x.MlxRunning, x => x.MlxModel,
                (r, m) => !r && !string.IsNullOrWhiteSpace(m)));
        StopMlxCommand = ReactiveCommand.Create(StopMlx,
            this.WhenAnyValue(x => x.MlxRunning));
        ClearMlxLogCommand = ReactiveCommand.Create(() => { MlxLog = string.Empty; });
        AddMlxEnvCommand = ReactiveCommand.Create(() =>
            MlxCustomEnv.Add(MakeEntry(MlxCustomEnv)));

        InstallMlxVlmCommand = ReactiveCommand.CreateFromTask(
            InstallMlxVlmAsync,
            this.WhenAnyValue(x => x.MlxVlmInstalled, x => x.MlxRunning, (i, r) => !i && !r));

        StartOpenWebUICommand = ReactiveCommand.Create(StartOpenWebUI,
            this.WhenAnyValue(x => x.OpenWebUIRunning, x => x.OpenWebUIInstalled, (r, i) => !r && i));
        StopOpenWebUICommand = ReactiveCommand.Create(StopOpenWebUI,
            this.WhenAnyValue(x => x.OpenWebUIRunning));
        InstallOpenWebUICommand = ReactiveCommand.CreateFromTask(InstallOpenWebUIAsync,
            this.WhenAnyValue(x => x.OpenWebUIInstalled, x => x.OpenWebUIRunning, (i, r) => !i && !r));
        OpenBrowserCommand = ReactiveCommand.Create(() =>
        {
            Process.Start(new ProcessStartInfo("open", $"http://localhost:{OpenWebUIPort}") { UseShellExecute = true });
        });
        ClearOpenWebUILogCommand = ReactiveCommand.Create(() => { OpenWebUILog = string.Empty; });
        AddOpenWebUIEnvCommand = ReactiveCommand.Create(() =>
            OpenWebUICustomEnv.Add(MakeEntry(OpenWebUICustomEnv)));

        _ = Task.Run(StartupInitAsync);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EnvEntry MakeEntry(ObservableCollection<EnvEntry> col, string key = "", string val = "")
    {
        var e = new EnvEntry { Key = key, Value = val };
        e.RemoveCommand = ReactiveCommand.Create(() =>
        {
            col.Remove(e);
            _envChanged.OnNext(Unit.Default);
        });
        e.PropertyChanged += (_, _) => _envChanged.OnNext(Unit.Default);
        return e;
    }

    private void LoadCustomEnv(string json, ObservableCollection<EnvEntry> col)
    {
        try
        {
            foreach (var kv in JsonSerializer.Deserialize<EnvKv[]>(json) ?? [])
                col.Add(MakeEntry(col, kv.Key, kv.Value));
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            _db.Set("OLLAMA_FLASH_ATTENTION", FlashAttention);
            _db.Set("OLLAMA_KV_CACHE_TYPE", KvCacheType);
            _db.Set("OLLAMA_NEW_ENGINE", NewEngine ? "true" : "false");
            _db.Set("OLLAMA_KEEP_ALIVE", KeepAlive);
            _db.Set("OLLAMA_CUSTOM_ENV", JsonSerializer.Serialize(
                OllamaCustomEnv.Select(e => new EnvKv(e.Key, e.Value))));

            _db.Set("MLX_MODEL", MlxModel);
            _db.Set("MLX_DRAFT_MODEL", MlxDraftModel);
            _db.Set("MLX_KV_BITS", MlxKvBits);
            _db.Set("MLX_ENGINE_MODE", MlxEngineMode);
            _db.Set("MLX_HOST", MlxHost);
            _db.Set("MLX_PORT", MlxPort);
            _db.Set("MLX_CUSTOM_ENV", JsonSerializer.Serialize(
                MlxCustomEnv.Select(e => new EnvKv(e.Key, e.Value))));

            _db.Set("OPENWEBUI_PORT", OpenWebUIPort);
            _db.Set("OPENWEBUI_CUSTOM_ENV", JsonSerializer.Serialize(
                OpenWebUICustomEnv.Select(e => new EnvKv(e.Key, e.Value))));
        }
        catch { }
    }

    // ── Data directories ──────────────────────────────────────────────────────

    private static string OllamaModelsDir
    {
        get
        {
            var dir = System.IO.Path.Combine(DatabaseService.AppSupportDir, "ollama", "models");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string MlxDataDir
    {
        get
        {
            var dir = System.IO.Path.Combine(DatabaseService.AppSupportDir, "mlx");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string OpenWebUIDataDir
    {
        get
        {
            var dir = System.IO.Path.Combine(DatabaseService.AppSupportDir, "open-webui");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ── Env builders ─────────────────────────────────────────────────────────

    private Dictionary<string, string> BuildOllamaEnv()
    {
        var env = new Dictionary<string, string>
        {
            ["OLLAMA_FLASH_ATTENTION"] = FlashAttention,
            ["OLLAMA_KV_CACHE_TYPE"]   = KvCacheType,
            ["OLLAMA_NEW_ENGINE"]      = NewEngine ? "true" : "false",
            ["OLLAMA_KEEP_ALIVE"]      = KeepAlive,
            ["OLLAMA_MODELS"]          = OllamaModelsDir,
        };
        foreach (var e in OllamaCustomEnv.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
            env[e.Key.Trim()] = e.Value;
        return env;
    }

    private Dictionary<string, string> BuildMlxEnv()
    {
        var xetDir = System.IO.Path.Combine(MlxDataDir, "xet");
        var env = new Dictionary<string, string>
        {
            ["HF_HOME"]      = MlxDataDir,
            ["HF_XET_CACHE"] = xetDir,
        };
        if (!string.IsNullOrWhiteSpace(HfToken))
            env["HUGGING_FACE_HUB_TOKEN"] = HfToken;
        foreach (var e in MlxCustomEnv.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
            env[e.Key.Trim()] = e.Value;
        return env;
    }

    private Dictionary<string, string> BuildOpenWebUIEnv()
    {
        var env = new Dictionary<string, string>
        {
            ["DATA_DIR"] = OpenWebUIDataDir,
        };
        foreach (var e in OpenWebUICustomEnv.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
            env[e.Key.Trim()] = e.Value;
        return env;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private async Task StartupInitAsync()
    {
        try
        {
            var ollamaPids = ManagedProcess.FindPidsByPort(11434);
            if (ollamaPids.Length > 0 && _ollama.Adopt(ollamaPids[0]))
            {
                for (int i = 1; i < ollamaPids.Length; i++) TryKillPid(ollamaPids[i]);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OllamaRunning = true;
                    AppendOllamaLog($"[ollama già in esecuzione — adottato (PID {ollamaPids[0]})]");
                });
            }

            if (int.TryParse(MlxPort, out var mlxPort))
            {
                var mlxPids = ManagedProcess.FindPidsByPort(mlxPort);
                if (mlxPids.Length > 0 && _mlx.Adopt(mlxPids[0]))
                {
                    for (int i = 1; i < mlxPids.Length; i++) TryKillPid(mlxPids[i]);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MlxRunning = true;
                        AppendMlxLog($"[mlx_lm.server già in esecuzione — adottato (PID {mlxPids[0]})]");
                    });
                }
            }

            if (int.TryParse(OpenWebUIPort, out var webuiPort))
            {
                var webuiPids = ManagedProcess.FindPidsByPort(webuiPort);
                if (webuiPids.Length > 0 && _openWebUI.Adopt(webuiPids[0]))
                {
                    for (int i = 1; i < webuiPids.Length; i++) TryKillPid(webuiPids[i]);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        OpenWebUIRunning = true;
                        AppendOpenWebUILog($"[open-webui già in esecuzione — adottato (PID {webuiPids[0]})]");
                    });
                }
            }
        }
        catch { }

        CheckOpenWebUIInstalled();
        CheckMlxVlmInstalled();
    }

    private static void TryKillPid(int pid)
    {
        try { Process.GetProcessById(pid).Kill(entireProcessTree: true); }
        catch { }
    }

    // ── Ollama actions ────────────────────────────────────────────────────────

    private void StartOllama()
    {
        foreach (var pid in ManagedProcess.FindPidsByPort(11434))
            TryKillPid(pid);
        try
        {
            _ollama.Start("ollama", "serve", BuildOllamaEnv());
            OllamaRunning = true;
        }
        catch (Exception ex)
        {
            AppendOllamaLog($"[errore avvio ollama: {ex.Message}]");
        }
    }

    private void StopOllama()
    {
        _ollama.Stop();
        OllamaRunning = false;
    }

    // ── MLX actions ───────────────────────────────────────────────────────────

    private void StartMlx()
    {
        if (int.TryParse(MlxPort, out var port))
            foreach (var pid in ManagedProcess.FindPidsByPort(port))
                TryKillPid(pid);
        try
        {
            var draft  = string.IsNullOrWhiteSpace(MlxDraftModel) ? "" : $" --draft-model {MlxDraftModel.Trim()}";
            var kvBits = string.IsNullOrWhiteSpace(MlxKvBits)    ? "" : $" --kv-bits {MlxKvBits.Trim()}";
            var isVlm = MlxEngineMode switch
            {
                "VLM" => true,
                "LM"  => false,
                _     => HuggingFaceService.IsVlmModel(MlxModel, MlxDataDir)
            };
            if (isVlm)
            {
                if (!MlxVlmInstalled)
                {
                    AppendMlxLog("[mlx-vlm non installato — usa il pulsante 'Installa VLM' prima di avviare]");
                    return;
                }
                // mlx_vlm.server supporta --kv-bits
                _mlx.Start("python3",
                    $"-m mlx_vlm.server --model {MlxModel} --port {MlxPort} --host {MlxHost}{draft}{kvBits}",
                    BuildMlxEnv(), MlxDataDir);
            }
            else
            {
                // mlx_lm.server NON supporta --kv-bits — ignorato
                _mlx.Start("mlx_lm.server",
                    $"--model {MlxModel} --port {MlxPort} --host {MlxHost}{draft}",
                    BuildMlxEnv(), MlxDataDir);
            }
            MlxRunning = true;
        }
        catch (Exception ex)
        {
            AppendMlxLog($"[errore avvio mlx: {ex.Message}]");
        }
    }

    private void StopMlx()
    {
        _mlx.Stop();
        MlxRunning = false;
    }

    // ── Open WebUI actions ────────────────────────────────────────────────────

    private void StartOpenWebUI()
    {
        if (int.TryParse(OpenWebUIPort, out var port))
            foreach (var pid in ManagedProcess.FindPidsByPort(port))
                TryKillPid(pid);
        try
        {
            _openWebUI.Start("open-webui", $"serve --port {OpenWebUIPort}", BuildOpenWebUIEnv(), OpenWebUIDataDir);
            OpenWebUIRunning = true;
        }
        catch (Exception ex)
        {
            AppendOpenWebUILog($"[errore avvio open-webui: {ex.Message}]");
        }
    }

    private void StopOpenWebUI()
    {
        _openWebUI.Stop();
        OpenWebUIRunning = false;
    }

    private async Task InstallOpenWebUIAsync()
    {
        AppendOpenWebUILog("Installazione open-webui in corso...");
        var tcs = new TaskCompletionSource<bool>();
        var installer = new ManagedProcess();
        installer.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendOpenWebUILog(line));
        installer.Exited += () => { CheckOpenWebUIInstalled(); tcs.TrySetResult(true); installer.Dispose(); };
        installer.Start("pip3", "install open-webui");
        await tcs.Task;
    }

    private void CheckMlxVlmInstalled()
    {
        Task.Run(() =>
        {
            bool installed;
            try
            {
                var psi = new ProcessStartInfo(ManagedProcess.ResolveCommand("pip3"), "show mlx-vlm")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
                psi.Environment["PATH"] = ManagedProcess.BuildPath();
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
                installed = p.ExitCode == 0;
            }
            catch { installed = false; }
            Dispatcher.UIThread.Post(() => MlxVlmInstalled = installed);
        });
    }

    private async Task InstallMlxVlmAsync()
    {
        AppendMlxLog("Installazione mlx-vlm in corso...");
        var tcs = new TaskCompletionSource<bool>();
        var installer = new ManagedProcess();
        installer.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendMlxLog(line));
        installer.Exited += () => { CheckMlxVlmInstalled(); tcs.TrySetResult(true); installer.Dispose(); };
        installer.Start("pip3", "install mlx-vlm");
        await tcs.Task;
    }

    private void CheckOpenWebUIInstalled()
    {
        Task.Run(() =>
        {
            bool installed;
            try
            {
                var psi = new ProcessStartInfo("which", "open-webui")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.Environment["PATH"] = ManagedProcess.BuildPath();
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                installed = p?.ExitCode == 0;
            }
            catch { installed = false; }
            Dispatcher.UIThread.Post(() => OpenWebUIInstalled = installed);
        });
    }

    // ── Log helpers ───────────────────────────────────────────────────────────

    private void AppendOllamaLog(string line)
    {
        var updated = OllamaLog + line + "\n";
        if (updated.Length > 60_000) updated = updated[^50_000..];
        OllamaLog = updated;
    }

    private void AppendMlxLog(string line)
    {
        var updated = MlxLog + line + "\n";
        if (updated.Length > 60_000) updated = updated[^50_000..];
        MlxLog = updated;
    }

    private void AppendOpenWebUILog(string line)
    {
        var updated = OpenWebUILog + line + "\n";
        if (updated.Length > 60_000) updated = updated[^50_000..];
        OpenWebUILog = updated;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _ollama.Dispose();
        _mlx.Dispose();
        _openWebUI.Dispose();
        _db.Dispose();
        _envChanged.Dispose();
    }
}
