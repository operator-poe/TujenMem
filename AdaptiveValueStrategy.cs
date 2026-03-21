using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TujenMem;

public class AdaptiveValueStrategy : IValueStrategy
{
    public ArtifactRatioTracker Tracker { get; private set; }

    private string JsonPath => Path.Combine(TujenMem.Instance.DirectoryFullName, "NinjaData", "RatioThresholds.json");

    private string StatisticsCsvPath
    {
        get
        {
            var league = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
            return Path.Combine(TujenMem.Instance.DirectoryFullName, "Statistics", $"{league}.csv");
        }
    }

    public AdaptiveValueStrategy()
    {
        var league = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
        Tracker = ArtifactRatioTracker.Initialize(JsonPath, StatisticsCsvPath, league);
    }

    public void EvaluateItems(List<HaggleItem> items)
    {
        var candidates = items
            .Where(i => i.State == HaggleItemState.Unpriced && i.Price != null && i.Price.Value > 0 && i.Value > 0)
            .Select(i => new
            {
                Item = i,
                Category = ArtifactRatioTracker.GetArtifactCategory(i.Price.Name),
                Ratio = i.Value / i.Price.Value
            })
            .Where(x => x.Category != null)
            .OrderByDescending(x => x.Ratio)
            .ToList();

        var budgets = new Dictionary<string, int>
        {
            { "Lesser", HaggleStock.Lesser },
            { "Greater", HaggleStock.Greater },
            { "Grand", HaggleStock.Grand },
            { "Exceptional", HaggleStock.Exceptional }
        };

        var multiplier = TujenMem.Instance.Settings.ArtifactValueSettings.ItemPriceMultiplier.Value;

        foreach (var c in candidates)
        {
            var threshold = Tracker.GetThreshold(c.Category);
            var adjustedThreshold = threshold * multiplier;

            if (c.Ratio >= adjustedThreshold && budgets[c.Category] >= c.Item.Price.Value)
            {
                c.Item.State = HaggleItemState.Priced;
                budgets[c.Category] -= c.Item.Price.Value;
                Log.Debug($"Adaptive BUY: {c.Item.Name} ratio={c.Ratio:F4} >= threshold={adjustedThreshold:F4} (base={threshold:F4} x {multiplier:F2}), cost={c.Item.Price.Value} {c.Category}");
            }
            else
            {
                c.Item.State = HaggleItemState.TooExpensive;
                var reason = c.Ratio < adjustedThreshold ? $"ratio={c.Ratio:F4} < threshold={adjustedThreshold:F4}" : $"budget={budgets[c.Category]} < cost={c.Item.Price.Value}";
                Log.Debug($"Adaptive SKIP: {c.Item.Name} {reason}");
            }
        }

        foreach (var item in items)
        {
            if (item.State == HaggleItemState.Unpriced)
                item.State = HaggleItemState.TooExpensive;
        }
    }

    public void OnWindowComplete(List<HaggleItem> items)
    {
        Tracker.UpdateFromWindow(items);
        Tracker.Save(JsonPath);
    }
}
