using System;

namespace TujenMem.ItemRoller;

public class CurrencyRound
{
    public string Name { get; init; }
    public string CurrencyName { get; init; }
    public string AlternativeCurrency { get; init; }

    /// <summary>
    /// Which items this round applies to (e.g. unidentified, magic rarity, etc.)
    /// </summary>
    public Func<ReRollable, bool> Applies { get; init; }

    /// <summary>
    /// Per-item stop condition. Return true when this round is done for the item.
    /// </summary>
    public Func<ReRollable, bool> IsComplete { get; init; }

    /// <summary>
    /// If true, hold the currency and click all applicable items in sequence.
    /// If false, pick up / apply / release per item.
    /// </summary>
    public bool BatchMode { get; init; }

    /// <summary>
    /// If true, re-apply the currency to each item until IsComplete returns true.
    /// </summary>
    public bool Iterative { get; init; }

    public int MaxIterations { get; init; } = 50;
}
