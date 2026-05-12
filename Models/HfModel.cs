using System.Windows.Input;
using ReactiveUI;

namespace OllamaManager.Models;

public class HfModel : ReactiveObject
{
    public string Id { get; init; } = "";
    public int Downloads { get; init; }

    private bool _isDownloaded;
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set => this.RaiseAndSetIfChanged(ref _isDownloaded, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
    }

    private long _diskSizeBytes;
    public long DiskSizeBytes
    {
        get => _diskSizeBytes;
        set
        {
            this.RaiseAndSetIfChanged(ref _diskSizeBytes, value);
            this.RaisePropertyChanged(nameof(DiskSizeFormatted));
        }
    }

    public string DiskSizeFormatted => DiskSizeBytes switch
    {
        >= 1_073_741_824 => $"{DiskSizeBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{DiskSizeBytes / 1_048_576.0:F1} MB",
        >= 1_024         => $"{DiskSizeBytes / 1_024.0:F1} KB",
        0                => "",
        _                => $"{DiskSizeBytes} B"
    };

    public string ShortName => Id.Contains('/') ? Id.Split('/')[^1] : Id;

    public string DownloadsFormatted => Downloads switch
    {
        >= 1_000_000 => $"{Downloads / 1_000_000.0:F1}M",
        >= 1_000     => $"{Downloads / 1_000.0:F1}k",
        _            => Downloads.ToString()
    };

    public ICommand? DownloadCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
    public ICommand? UseCommand { get; set; }
}
