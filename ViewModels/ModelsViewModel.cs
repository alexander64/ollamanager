using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using OllamaManager.Models;
using OllamaManager.Services;
using ReactiveUI;

namespace OllamaManager.ViewModels;

public class ModelsViewModel : ReactiveObject
{
    private readonly MainViewModel _main;
    private readonly HuggingFaceService _hf;
    private List<HfModel> _allMlxModels = new();

    private static string MlxDataDir
    {
        get
        {
            var dir = Path.Combine(DatabaseService.AppSupportDir, "mlx");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ── MLX Models ────────────────────────────────────────────────────────────

    public ObservableCollection<HfModel> FilteredMlxModels { get; } = new();
    public ObservableCollection<string> DownloadedMlxModelIds { get; } = new();

    private string _mlxSearchText = "";
    public string MlxSearchText
    {
        get => _mlxSearchText;
        set => this.RaiseAndSetIfChanged(ref _mlxSearchText, value);
    }

    private bool _showOnlyDownloaded;
    public bool ShowOnlyDownloaded
    {
        get => _showOnlyDownloaded;
        set => this.RaiseAndSetIfChanged(ref _showOnlyDownloaded, value);
    }

    private bool _isLoadingMlx;
    public bool IsLoadingMlx
    {
        get => _isLoadingMlx;
        set => this.RaiseAndSetIfChanged(ref _isLoadingMlx, value);
    }

    private string _mlxError = "";
    public string MlxError
    {
        get => _mlxError;
        set => this.RaiseAndSetIfChanged(ref _mlxError, value);
    }

    private string _mlxDownloadLog = "";
    public string MlxDownloadLog
    {
        get => _mlxDownloadLog;
        set => this.RaiseAndSetIfChanged(ref _mlxDownloadLog, value);
    }

    // ── Ollama Models ─────────────────────────────────────────────────────────

    public ObservableCollection<OllamaModel> OllamaModels { get; } = new();

    private bool _isLoadingOllama;
    public bool IsLoadingOllama
    {
        get => _isLoadingOllama;
        set => this.RaiseAndSetIfChanged(ref _isLoadingOllama, value);
    }

    private string _ollamaPullModel = "";
    public string OllamaPullModel
    {
        get => _ollamaPullModel;
        set => this.RaiseAndSetIfChanged(ref _ollamaPullModel, value);
    }

    private string _ollamaLog = "";
    public string OllamaLog
    {
        get => _ollamaLog;
        set => this.RaiseAndSetIfChanged(ref _ollamaLog, value);
    }

    private bool _isOllamaPulling;
    public bool IsOllamaPulling
    {
        get => _isOllamaPulling;
        set => this.RaiseAndSetIfChanged(ref _isOllamaPulling, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ReactiveCommand<Unit, Unit> RefreshMlxCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshOllamaCommand { get; }
    public ReactiveCommand<Unit, Unit> PullOllamaModelCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearMlxDownloadLogCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearOllamaLogCommand { get; }

    public ModelsViewModel(MainViewModel main, HuggingFaceService hf)
    {
        _main = main;
        _hf = hf;

        this.WhenAnyValue(x => x.MlxSearchText, x => x.ShowOnlyDownloaded)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        RefreshMlxCommand = ReactiveCommand.CreateFromTask(
            LoadMlxModelsAsync,
            this.WhenAnyValue(x => x.IsLoadingMlx, l => !l));

        RefreshOllamaCommand = ReactiveCommand.CreateFromTask(
            LoadOllamaModelsAsync,
            this.WhenAnyValue(x => x.IsLoadingOllama, l => !l));

        PullOllamaModelCommand = ReactiveCommand.CreateFromTask(
            PullOllamaModelAsync,
            this.WhenAnyValue(x => x.OllamaPullModel, x => x.IsOllamaPulling,
                (m, p) => !string.IsNullOrWhiteSpace(m) && !p));

        ClearMlxDownloadLogCommand = ReactiveCommand.Create(() => { MlxDownloadLog = ""; });
        ClearOllamaLogCommand      = ReactiveCommand.Create(() => { OllamaLog = ""; });

        _ = Task.Run(async () =>
        {
            await LoadMlxModelsAsync();
            await LoadOllamaModelsAsync();
        });
    }

    // ── MLX ───────────────────────────────────────────────────────────────────

    private async Task LoadMlxModelsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { IsLoadingMlx = true; MlxError = ""; });
        try
        {
            var infos = await _hf.GetMlxModelsAsync();
            var models = infos.Select(info =>
            {
                var downloaded = HuggingFaceService.IsModelDownloaded(info.Id, MlxDataDir);
                var m = new HfModel
                {
                    Id           = info.Id,
                    Downloads    = info.Downloads,
                    IsDownloaded = downloaded,
                    DiskSizeBytes = downloaded ? HuggingFaceService.GetModelDiskSize(info.Id, MlxDataDir) : 0,
                };
                m.DownloadCommand = ReactiveCommand.CreateFromTask(
                    () => DownloadModelAsync(m),
                    m.WhenAnyValue(x => x.IsDownloading, x => x.IsDownloaded,
                        (dl, dd) => !dl && !dd));
                m.DeleteCommand = ReactiveCommand.Create(
                    () => DeleteModel(m),
                    m.WhenAnyValue(x => x.IsDownloaded, x => x.IsDownloading,
                        (dd, dl) => dd && !dl));
                m.UseCommand = ReactiveCommand.Create(
                    () => { _main.MlxModel = m.Id; _main.SelectedPage = 0; },
                    m.WhenAnyValue(x => x.IsDownloaded));
                return m;
            }).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allMlxModels = models;
                ApplyFilter();
                IsLoadingMlx = false;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MlxError = $"Errore caricamento: {ex.Message}";
                IsLoadingMlx = false;
            });
        }
    }

    private async Task DownloadModelAsync(HfModel model)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            model.IsDownloading = true;
            AppendMlxLog($"[download {model.Id}...]");
        });

        var tcs = new TaskCompletionSource();
        var proc = new ManagedProcess();
        proc.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendMlxLog(line));
        proc.Exited += () => tcs.TrySetResult();

        var env = new Dictionary<string, string>
        {
            ["HF_HOME"]      = MlxDataDir,
            ["HF_XET_CACHE"] = System.IO.Path.Combine(MlxDataDir, "xet"),
        };
        if (!string.IsNullOrWhiteSpace(_main.HfToken))
            env["HUGGING_FACE_HUB_TOKEN"] = _main.HfToken;

        try
        {
            proc.Start("hf", $"download {model.Id}", env);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AppendMlxLog($"[errore avvio download: {ex.Message}]");
                model.IsDownloading = false;
            });
            proc.Dispose();
            return;
        }

        await tcs.Task;
        proc.Dispose();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            model.IsDownloaded  = HuggingFaceService.IsModelDownloaded(model.Id, MlxDataDir);
            model.DiskSizeBytes = model.IsDownloaded ? HuggingFaceService.GetModelDiskSize(model.Id, MlxDataDir) : 0;
            model.IsDownloading = false;
            AppendMlxLog(model.IsDownloaded ? $"[{model.Id} pronto]" : $"[download {model.Id} fallito]");
            ApplyFilter();
        });
    }

    private void DeleteModel(HfModel model)
    {
        try { HuggingFaceService.DeleteModel(model.Id, MlxDataDir); }
        catch (Exception ex) { AppendMlxLog($"[errore eliminazione: {ex.Message}]"); return; }
        model.IsDownloaded = false;
        AppendMlxLog($"[{model.Id} eliminato]");
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredMlxModels.Clear();
        DownloadedMlxModelIds.Clear();

        var search = MlxSearchText.ToLower();
        foreach (var m in _allMlxModels)
        {
            if (m.IsDownloaded)
                DownloadedMlxModelIds.Add(m.Id);

            if (ShowOnlyDownloaded && !m.IsDownloaded) continue;
            if (search.Length > 0 && !m.Id.ToLower().Contains(search)) continue;
            FilteredMlxModels.Add(m);
        }
    }

    private void AppendMlxLog(string line)
    {
        var updated = MlxDownloadLog + line + "\n";
        if (updated.Length > 60_000) updated = updated[^50_000..];
        MlxDownloadLog = updated;
    }

    // ── Ollama ────────────────────────────────────────────────────────────────

    private async Task LoadOllamaModelsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsLoadingOllama = true);
        try
        {
            var output = await RunCommandAsync("ollama", "list");
            var models = ParseOllamaList(output);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OllamaModels.Clear();
                foreach (var m in models)
                {
                    m.DeleteCommand = ReactiveCommand.CreateFromTask(() => DeleteOllamaModelAsync(m.Name));
                    OllamaModels.Add(m);
                }
                IsLoadingOllama = false;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AppendOllamaLog($"[errore lista modelli: {ex.Message}]");
                IsLoadingOllama = false;
            });
        }
    }

    private static List<OllamaModel> ParseOllamaList(string output)
    {
        var list = new List<OllamaModel>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                list.Add(new OllamaModel { Name = parts[0], Size = $"{parts[2]} {(parts.Length > 3 ? parts[3] : "")}".Trim() });
        }
        return list;
    }

    private async Task DeleteOllamaModelAsync(string name)
    {
        await Dispatcher.UIThread.InvokeAsync(() => AppendOllamaLog($"[eliminazione {name}...]"));
        await RunCommandAsync("ollama", $"rm {name}");
        await Dispatcher.UIThread.InvokeAsync(() => AppendOllamaLog($"[{name} eliminato]"));
        await LoadOllamaModelsAsync();
    }

    private async Task PullOllamaModelAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsOllamaPulling = true;
            AppendOllamaLog($"[pull {OllamaPullModel}...]");
        });

        var tcs = new TaskCompletionSource();
        var proc = new ManagedProcess();
        proc.OutputReceived += line => Dispatcher.UIThread.Post(() => AppendOllamaLog(line));
        proc.Exited += () => tcs.TrySetResult();

        try
        {
            proc.Start("ollama", $"pull {OllamaPullModel}");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AppendOllamaLog($"[errore: {ex.Message}]");
                IsOllamaPulling = false;
            });
            proc.Dispose();
            return;
        }

        await tcs.Task;
        proc.Dispose();

        await LoadOllamaModelsAsync();
        await Dispatcher.UIThread.InvokeAsync(() => IsOllamaPulling = false);
    }

    private void AppendOllamaLog(string line)
    {
        var updated = OllamaLog + line + "\n";
        if (updated.Length > 60_000) updated = updated[^50_000..];
        OllamaLog = updated;
    }

    private static async Task<string> RunCommandAsync(string command, string args)
    {
        var psi = new ProcessStartInfo(ManagedProcess.ResolveCommand(command), args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        psi.Environment["PATH"] = ManagedProcess.BuildPath();
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output;
    }
}
