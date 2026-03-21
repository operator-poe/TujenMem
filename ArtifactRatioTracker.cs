using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TujenMem;

public class ArtifactRatioTracker
{
    public string League { get; set; } = "";
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public Dictionary<string, float> Thresholds { get; set; } = new();
    public Dictionary<string, int> SampleCounts { get; set; } = new();

    private static readonly Dictionary<string, float> SeedDefaults = new()
    {
        { "Lesser", 0.012f },
        { "Greater", 0.015f },
        { "Grand", 0.020f },
        { "Exceptional", 0.050f }
    };

    private static readonly string[] ArtifactTypes = { "Lesser", "Greater", "Grand", "Exceptional" };

    public static string GetArtifactCategory(string artifactName)
    {
        if (artifactName.Contains("Lesser")) return "Lesser";
        if (artifactName.Contains("Greater")) return "Greater";
        if (artifactName.Contains("Grand")) return "Grand";
        if (artifactName.Contains("Exceptional")) return "Exceptional";
        return null;
    }

    public float GetThreshold(string artifactType)
    {
        if (Thresholds.TryGetValue(artifactType, out var threshold))
            return threshold;
        if (SeedDefaults.TryGetValue(artifactType, out var seed))
            return seed;
        return 0.01f;
    }

    public int GetSampleCount(string artifactType)
    {
        return SampleCounts.TryGetValue(artifactType, out var count) ? count : 0;
    }

    public void Update(string artifactType, float observedRatio)
    {
        if (string.IsNullOrEmpty(artifactType) || observedRatio <= 0)
            return;

        var samples = GetSampleCount(artifactType);
        var alpha = samples < 50 ? 0.3f : samples < 200 ? 0.15f : 0.1f;

        var current = GetThreshold(artifactType);
        Thresholds[artifactType] = alpha * observedRatio + (1f - alpha) * current;
        SampleCounts[artifactType] = samples + 1;
        LastUpdated = DateTime.Now;
    }

    public void UpdateFromWindow(List<HaggleItem> items)
    {
        var ratiosByType = new Dictionary<string, List<float>>();

        foreach (var item in items)
        {
            if (item.State != HaggleItemState.Priced && item.State != HaggleItemState.TooExpensive)
                continue;
            if (item.Price == null || item.Price.Value <= 0 || item.Value <= 0)
                continue;

            var category = GetArtifactCategory(item.Price.Name);
            if (category == null)
                continue;

            var ratio = item.Value / item.Price.Value;

            if (!ratiosByType.ContainsKey(category))
                ratiosByType[category] = new List<float>();
            ratiosByType[category].Add(ratio);
        }

        foreach (var (type, ratios) in ratiosByType)
        {
            ratios.Sort();
            var median = ratios[ratios.Count / 2];
            Update(type, median);
        }
    }

    public void BootstrapFromStatistics(string csvPath)
    {
        if (!File.Exists(csvPath))
            return;

        try
        {
            var ratiosByType = new Dictionary<string, List<float>>();
            var lines = File.ReadAllLines(csvPath);

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(';');
                if (cols.Length < 10)
                    continue;

                if (cols[9] != "Priced")
                    continue;

                if (!float.TryParse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var chaosValue))
                    continue;
                if (!float.TryParse(cols[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var artifactPrice))
                    continue;
                if (!float.TryParse(cols[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
                    continue;

                if (artifactPrice <= 0 || chaosValue <= 0)
                    continue;

                var totalChaos = chaosValue * amount;
                var ratio = totalChaos / artifactPrice;

                var category = GetArtifactCategory(cols[6]);
                if (category == null)
                    continue;

                if (!ratiosByType.ContainsKey(category))
                    ratiosByType[category] = new List<float>();
                ratiosByType[category].Add(ratio);
            }

            foreach (var (type, ratios) in ratiosByType)
            {
                if (ratios.Count < 10)
                    continue;

                ratios.Sort();
                var p25 = ratios[(int)(ratios.Count * 0.25)];
                Thresholds[type] = p25;
                SampleCounts[type] = ratios.Count;
            }

            LastUpdated = DateTime.Now;
            Log.Debug($"Bootstrapped thresholds from {csvPath}: {string.Join(", ", Thresholds.Select(kv => $"{kv.Key}={kv.Value:F4}"))}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to bootstrap from statistics: {e.Message}");
        }
    }

    public void EnsureAllTypes()
    {
        foreach (var type in ArtifactTypes)
        {
            if (!Thresholds.ContainsKey(type))
                Thresholds[type] = SeedDefaults[type];
            if (!SampleCounts.ContainsKey(type))
                SampleCounts[type] = 0;
        }
    }

    public void Save(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
            Log.Debug($"Saved ratio thresholds to {path}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save ratio thresholds: {e.Message}");
        }
    }

    public static ArtifactRatioTracker Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var tracker = JsonConvert.DeserializeObject<ArtifactRatioTracker>(json);
                if (tracker != null)
                {
                    tracker.EnsureAllTypes();
                    Log.Debug($"Loaded ratio thresholds from {path}: {string.Join(", ", tracker.Thresholds.Select(kv => $"{kv.Key}={kv.Value:F4}"))}");
                    return tracker;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load ratio thresholds: {e.Message}");
        }
        return null;
    }

    public static ArtifactRatioTracker Initialize(string jsonPath, string statisticsCsvPath, string league)
    {
        var existing = Load(jsonPath);
        if (existing != null && existing.League == league)
        {
            Log.Debug("Using existing ratio thresholds (league match).");
            return existing;
        }

        var tracker = new ArtifactRatioTracker { League = league };
        tracker.EnsureAllTypes();

        if (File.Exists(statisticsCsvPath))
        {
            Log.Debug("Bootstrapping ratio thresholds from statistics CSV...");
            tracker.BootstrapFromStatistics(statisticsCsvPath);
            tracker.Save(jsonPath);
        }
        else
        {
            Log.Debug("No statistics data found. Using seed defaults.");
        }

        return tracker;
    }
}
