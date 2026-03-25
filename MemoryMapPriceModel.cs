using System;
using ExileCore.PoEMemory.MemoryObjects;
using ItemFilterLibrary;

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
    private const double Intercept = 3.211559;
    private const double BetaQty = 0.008037;
    private const double BetaQtySq = 0.003298;
    private const double BetaRar = 0.000947;
    private const double BetaRarHigh = 0.014042;
    private const double BetaPs = 0.058860;
    private const double BetaMaps = 0.005253;
    private const double BetaCurr = 0.013576;
    private const double BetaRarCurr = 0.508597;  // rarity × currency interaction
    private const double BetaCurrHi = 0.219179;  // premium for stacked currency (≥90)

    /// <summary>
    /// Estimate the price of a Memory-influenced map in chaos orbs.
    /// scarcityWeight controls the log-ceiling premium for stats approaching
    /// their practical max. 0 = disabled, 0.15 = gentle, 0.3 = aggressive.
    /// Threshold/ceiling params control when scarcity kicks in and what the
    /// practical maximum is for each stat.
    /// </summary>
    public static double EstimatePrice(int quantity, int rarity, int packSize,
                                        int moreMaps, int moreCurrency,
                                        double scarcityWeight = 0,
                                        int currencyThreshold = 85,
                                        int rarityThreshold = 150,
                                        int quantityThreshold = 115,
                                        int currencyCeiling = 320,
                                        int rarityCeiling = 400,
                                        int quantityCeiling = 160)
    {
        double dq = quantity - 110.0;
        double dr = rarity - 120.0;
        // Soft-cap pack size: the regression coefficient (0.059/unit) was trained
        // on data with pack size 30–65. Beyond ~55, apply sqrt diminishing returns
        // so extreme values (80+) don't dominate the estimate.
        double rawDps = packSize - 40.0;
        double dps = rawDps <= 15 ? rawDps : 15.0 + Math.Sqrt(Math.Max(0, rawDps - 15.0)) * 4.0;
        double rarHigh = Math.Max(0, rarity - 200);
        double currHigh = moreCurrency >= 90 ? 1.0 : 0.0;

        double logPrice = Intercept
            + BetaQty * dq
            + BetaQtySq * (dq * dq / 100.0)
            + BetaRar * dr
            + BetaRarHigh * rarHigh
            + BetaPs * dps
            + BetaMaps * moreMaps
            + BetaCurr * moreCurrency
            + BetaRarCurr * (rarity * moreCurrency / 10000.0)
            + BetaCurrHi * currHigh;

        double basePrice = Math.Exp(logPrice);

        // Log-ceiling scarcity premium: as a stat approaches its practical
        // maximum, supply drops and price accelerates.
        // Formula: multiplier = 1 + weight * ln(ceiling / (ceiling - stat))
        // At stat=0: multiplier=1. At 90% of ceiling: ~1 + weight*2.3.
        if (scarcityWeight > 0)
        {
            double scarcity = 1.0;
            if (moreCurrency >= currencyThreshold)
                scarcity *= 1.0 + scarcityWeight * Math.Log((double)currencyCeiling / Math.Max(1, currencyCeiling - moreCurrency));
            if (rarity >= rarityThreshold)
                scarcity *= 1.0 + scarcityWeight * Math.Log((double)rarityCeiling / Math.Max(1, rarityCeiling - rarity));
            if (quantity >= quantityThreshold)
                scarcity *= 1.0 + scarcityWeight * Math.Log((double)quantityCeiling / Math.Max(1, quantityCeiling - quantity));
            basePrice *= scarcity;
        }

        return basePrice;
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
        double halfSpread = Math.Log(2.0); // ≈ 0.693 → range is [est/2, est*2]
        double logLow = logEstimate - halfSpread;
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
            return $"{chaos:F0}c ({chaos / divineRate:F1}d)";
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

        foreach (var mod in mods.ImplicitMods)
        {
            var name = mod.Name ?? "";
            if (name.Contains("MapZanaInfluenced", StringComparison.OrdinalIgnoreCase))
                return true;
        }


        // Text fallback
        if (mods.HumanImpStats != null)
        {
            foreach (var stat in mods.HumanImpStats)
            {
                if (stat != null && stat.Contains("Originator", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extract all pricing-relevant stats from a map entity
    /// using ItemData.MapInfo from ItemFilterLibrary.
    /// </summary>
    public static MemoryMapStats ExtractStats(Entity item)
    {
        if (item == null) return null;

        try
        {
            var iData = new ItemData(item, TujenMem.Instance.GameController);
            var d = iData.MapInfo;
            if (d == null) return null;

            return new MemoryMapStats
            {
                Quantity = d.Quantity,
                Rarity = d.Rarity,
                PackSize = d.PackSize,
                MoreMaps = d.MoreMaps,
                MoreScarabs = d.MoreScarabs,
                MoreCurrency = d.MoreCurrency,
            };
        }
        catch
        {
            return null;
        }
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
