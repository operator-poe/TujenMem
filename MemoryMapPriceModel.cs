using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore.PoEMemory.MemoryObjects;

namespace TujenMem;

/// <summary>
/// Pricing model for Memory-influenced corrupted T16 maps.
///
/// Derived from robust log-linear regression (IRLS with Huber weights)
/// on ~240 trade listings with non-linear rarity/quantity terms and
/// a rarity×currency interaction capturing the multiplicative synergy
/// between high item rarity and "more currency drops".
///
/// Model: log(price_chaos) = intercept
///   + β_qty × (qty - 110) + β_qty² × (qty - 110)² / 100
///   + β_rar × (rar - 120) + β_rarHigh × max(0, rar - 200)
///   + β_ps  × (ps - 40)
///   + β_maps × moreMaps
///   + β_curr × moreCurrency
///   + β_interaction × (rar × moreCurrency / 10000)
///   + β_currHigh × I(moreCurrency ≥ 90)
/// </summary>
public static class MemoryMapPriceModel
{
    // Regression coefficients (IRLS-estimated, robust to outliers)
    private const double Intercept   =  3.211559;
    private const double BetaQty     =  0.008037;
    private const double BetaQtySq   =  0.003298;
    private const double BetaRar     =  0.000947;
    private const double BetaRarHigh =  0.014042;
    private const double BetaPs      =  0.058860;
    private const double BetaMaps    =  0.005253;
    private const double BetaCurr    =  0.013576;
    private const double BetaRarCurr =  0.508597;  // rarity × currency interaction
    private const double BetaCurrHi  =  0.219179;  // premium for stacked currency (≥90)

    /// <summary>
    /// Estimate the price of a Memory-influenced map in chaos orbs.
    /// </summary>
    public static double EstimatePrice(int quantity, int rarity, int packSize,
                                        int moreMaps, int moreCurrency)
    {
        double dq  = quantity - 110.0;
        double dr  = rarity - 120.0;
        double dps = packSize - 40.0;
        double rarHigh  = Math.Max(0, rarity - 200);
        double currHigh = moreCurrency >= 90 ? 1.0 : 0.0;

        double logPrice = Intercept
            + BetaQty     * dq
            + BetaQtySq   * (dq * dq / 100.0)
            + BetaRar     * dr
            + BetaRarHigh * rarHigh
            + BetaPs      * dps
            + BetaMaps    * moreMaps
            + BetaCurr    * moreCurrency
            + BetaRarCurr * (rarity * moreCurrency / 10000.0)
            + BetaCurrHi  * currHigh;

        return Math.Exp(logPrice);
    }

    /// <summary>
    /// Compute a display price given model estimate, discount, and percentile.
    /// percentile 0 = low end of range, 50 = midpoint, 100 = high end.
    /// </summary>
    public static double AdjustedPrice(double rawEstimate, double discountPct, double percentile)
    {
        double discount = 1.0 - discountPct / 100.0;
        // Model RMSE in log-space ≈ 1.12, so ±1σ is roughly ÷3 to ×3.
        // We use a tighter practical range of ÷2 to ×2 for display.
        double logEstimate = Math.Log(rawEstimate * discount);
        double halfSpread  = Math.Log(2.0); // ≈ 0.693 → range is [est/2, est*2]
        double logLow  = logEstimate - halfSpread;
        double logHigh = logEstimate + halfSpread;
        double logDisplay = logLow + (percentile / 100.0) * (logHigh - logLow);
        return Math.Exp(logDisplay);
    }

    /// <summary>
    /// Format a chaos value as a human-readable string (e.g. "1.2d" or "85c").
    /// </summary>
    public static string FormatPrice(double chaos, double divineRate = 260)
    {
        if (chaos >= divineRate)
            return $"{chaos / divineRate:F1}d";
        return $"{chaos:F0}c";
    }

    // ─── Stat extraction from item entities ───────────────────────────

    /// <summary>
    /// Check whether an entity is a Memory-influenced map.
    /// </summary>
    public static bool IsMemoryInfluencedMap(Entity item)
    {
        if (item == null) return false;

        var mapKey = item.GetComponent<ExileCore.PoEMemory.Components.MapKey>();
        if (mapKey == null) return false;

        var baseComp = item.GetComponent<ExileCore.PoEMemory.Components.Base>();
        if (baseComp == null || !baseComp.isCorrupted) return false;

        var mods = item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        if (mods?.ItemMods == null) return false;

        // Look for the Memory-influence implicit in item mods.
        // The mod name typically contains "MemoryLine" or "UberMap" or similar.
        // Fallback: check HumanStats for "Influenced by the Originator's Memories".
        foreach (var mod in mods.ItemMods)
        {
            var name = mod.Name ?? "";
            if (name.Contains("MemoryLine", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("UberMap", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MemoryInfluence", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Text fallback
        if (mods.HumanStats != null)
        {
            foreach (var stat in mods.HumanStats)
            {
                if (stat != null && stat.Contains("Originator", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extract all pricing-relevant stats from a map entity.
    /// Returns null if stats cannot be read.
    /// </summary>
    public static MemoryMapStats ExtractStats(Entity item)
    {
        if (item == null) return null;

        var mods = item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        if (mods?.HumanStats == null) return null;

        var stats = new MemoryMapStats();

        foreach (var stat in mods.HumanStats)
        {
            if (stat == null) continue;
            var lower = stat.ToLowerInvariant();

            // Quantity: "+95% increased Item Quantity" or "Item Quantity: +95%"
            var qtyMatch = Regex.Match(stat, @"\+?(\d+)%.*(?:item\s+)?quantity", RegexOptions.IgnoreCase);
            if (qtyMatch.Success)
                stats.Quantity = Math.Max(stats.Quantity, int.Parse(qtyMatch.Groups[1].Value));

            // Rarity: "+165% increased Item Rarity" or "Item Rarity: +165%"
            var rarMatch = Regex.Match(stat, @"\+?(\d+)%.*(?:item\s+)?rarity", RegexOptions.IgnoreCase);
            if (rarMatch.Success)
                stats.Rarity = Math.Max(stats.Rarity, int.Parse(rarMatch.Groups[1].Value));

            // Pack Size: "+42% Monster pack size"
            var psMatch = Regex.Match(stat, @"\+?(\d+)%.*pack\s*size", RegexOptions.IgnoreCase);
            if (psMatch.Success)
                stats.PackSize = Math.Max(stats.PackSize, int.Parse(psMatch.Groups[1].Value));

            // More Maps: "35% more Maps found in Area"
            var mapsMatch = Regex.Match(stat, @"(\d+)%\s+more\s+map", RegexOptions.IgnoreCase);
            if (mapsMatch.Success)
                stats.MoreMaps += int.Parse(mapsMatch.Groups[1].Value);

            // More Scarabs: "60% more Scarabs found in Area"
            var scarMatch = Regex.Match(stat, @"(\d+)%\s+more\s+scarab", RegexOptions.IgnoreCase);
            if (scarMatch.Success)
                stats.MoreScarabs += int.Parse(scarMatch.Groups[1].Value);

            // More Currency: "47% more Currency found in Area"
            var currMatch = Regex.Match(stat, @"(\d+)%\s+more\s+currency", RegexOptions.IgnoreCase);
            if (currMatch.Success)
                stats.MoreCurrency += int.Parse(currMatch.Groups[1].Value);
        }

        return stats;
    }
}

public class MemoryMapStats
{
    public int Quantity { get; set; }
    public int Rarity { get; set; }
    public int PackSize { get; set; }
    public int MoreMaps { get; set; }
    public int MoreScarabs { get; set; }
    public int MoreCurrency { get; set; }
}
