using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OllamaManager.Services;

public record HfModelInfo(string Id, int Downloads);

public class HuggingFaceService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string ApiBase = "https://huggingface.co/api";

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    public async Task<List<HfModelInfo>> GetMlxModelsAsync(int limit = 500)
    {
        var url = $"{ApiBase}/models?author=mlx-community&limit={limit}&sort=downloads&direction=-1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "OllamaManager/1.0");
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var list = new List<HfModelInfo>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var dl = el.TryGetProperty("downloads", out var dlProp) ? dlProp.GetInt32() : 0;
            if (!string.IsNullOrEmpty(id))
                list.Add(new HfModelInfo(id, dl));
        }
        return list;
    }

    public static bool IsModelDownloaded(string modelId, string hfHome)
    {
        var cacheName = "models--" + modelId.Replace("/", "--");
        var snapshots = Path.Combine(hfHome, "hub", cacheName, "snapshots");
        try
        {
            if (!Directory.Exists(snapshots)) return false;
            var snapshotDirs = Directory.GetDirectories(snapshots);
            if (snapshotDirs.Length == 0) return false;

            // Count all files in the snapshot (following symlinks)
            var files = snapshotDirs
                .SelectMany(dir => Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                .ToList();

            // Traditional storage: weight file present
            if (files.Any(f => Path.GetExtension(f).ToLower() is ".safetensors" or ".npz" or ".bin" or ".gguf"))
                return true;

            // XET storage: weights are in chunk cache — snapshot has ≥ 3 files
            // (config.json + tokenizer + at least one weight pointer)
            return files.Count >= 3;
        }
        catch { return false; }
    }

    public static long GetModelDiskSize(string modelId, string hfHome)
    {
        // Scan the blobs/ directory, not snapshots/ — snapshots contain symlinks
        // whose FileInfo.Length on macOS/.NET returns the symlink size, not the target
        var cacheName = "models--" + modelId.Replace("/", "--");
        var blobsDir  = Path.Combine(hfHome, "hub", cacheName, "blobs");
        try
        {
            if (!Directory.Exists(blobsDir)) return 0;
            return Directory.GetFiles(blobsDir)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        }
        catch { return 0; }
    }

    public static void DeleteModel(string modelId, string hfHome)
    {
        var cacheName = "models--" + modelId.Replace("/", "--");
        var modelDir = Path.Combine(hfHome, "hub", cacheName);
        if (Directory.Exists(modelDir))
            Directory.Delete(modelDir, recursive: true);
    }

    public static bool IsVlmModel(string modelId, string hfHome)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        var cacheName = "models--" + modelId.Replace("/", "--");
        var snapshots = Path.Combine(hfHome, "hub", cacheName, "snapshots");
        if (!Directory.Exists(snapshots)) return false;
        var latest = Directory.GetDirectories(snapshots).OrderByDescending(d => d).FirstOrDefault();
        if (latest == null) return false;
        var configPath = Path.Combine(latest, "config.json");
        if (!File.Exists(configPath)) return false;
        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // vision_config present and non-null → definitely VLM
            if (root.TryGetProperty("vision_config", out var vc) && vc.ValueKind != JsonValueKind.Null)
                return true;

            // model_type check: covers qwen2_5_vl, qwen3_vl, llava*, paligemma, etc.
            if (root.TryGetProperty("model_type", out var mt))
            {
                var modelType = mt.GetString() ?? "";
                if (modelType.Contains("vl", StringComparison.OrdinalIgnoreCase) ||
                    modelType is "llava" or "llava_next" or "paligemma" or
                                "idefics2" or "idefics3" or "minicpmv" or "internvl")
                    return true;
            }

            // boi/audio token ids present and non-null → VLM
            if (root.TryGetProperty("boi_token_id", out var boi) && boi.ValueKind != JsonValueKind.Null)
                return true;
            if (root.TryGetProperty("audio_token_id", out var audio) && audio.ValueKind != JsonValueKind.Null)
                return true;

            return false;
        }
        catch { return false; }
    }
}
