using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace TujenMem;

[Submenu(CollapsedByDefault = false)]
public class MemoryMapPriceSettings
{
    [Menu("Enable Overlay", "Show price estimates on Memory-influenced corrupted maps")]
    public ToggleNode EnableOverlay { get; set; } = new ToggleNode(true);

    [Menu("Discount %", "Reduce estimate by this percentage (listing prices are above sale prices)")]
    public RangeNode<int> DiscountPct { get; set; } = new RangeNode<int>(15, 0, 50);

    [Menu("Percentile", "Where in the estimated range to show (0 = low, 50 = mid, 100 = high)")]
    public RangeNode<int> Percentile { get; set; } = new RangeNode<int>(50, 0, 100);

    [Menu("Scarcity Weight", "Log-ceiling premium for stats near their max (0 = off, 15 = gentle, 30 = aggressive)")]
    public RangeNode<int> ScarcityWeight { get; set; } = new RangeNode<int>(15, 0, 50);

    [Menu("Currency Scarcity Threshold", "moreCurrency must reach this before scarcity premium kicks in")]
    public RangeNode<int> CurrencyThreshold { get; set; } = new RangeNode<int>(85, 0, 200);

    [Menu("Rarity Scarcity Threshold", "Rarity must reach this before scarcity premium kicks in")]
    public RangeNode<int> RarityThreshold { get; set; } = new RangeNode<int>(150, 50, 350);

    [Menu("Quantity Scarcity Threshold", "Quantity must reach this before scarcity premium kicks in")]
    public RangeNode<int> QuantityThreshold { get; set; } = new RangeNode<int>(115, 50, 160);

    [Menu("Currency Ceiling", "Practical max for moreCurrency (scarcity goes to infinity at ceiling)")]
    public RangeNode<int> CurrencyCeiling { get; set; } = new RangeNode<int>(320, 150, 600);

    [Menu("Rarity Ceiling", "Practical max for rarity")]
    public RangeNode<int> RarityCeiling { get; set; } = new RangeNode<int>(400, 200, 800);

    [Menu("Quantity Ceiling", "Practical max for quantity")]
    public RangeNode<int> QuantityCeiling { get; set; } = new RangeNode<int>(160, 120, 250);

    [Menu("Divine Rate", "Current divine-to-chaos exchange rate")]
    public RangeNode<int> DivineRate { get; set; } = new RangeNode<int>(260, 100, 1000);

    [Menu("Show on Inventory", "Show overlay on maps in player inventory")]
    public ToggleNode ShowOnInventory { get; set; } = new ToggleNode(true);

    [Menu("Show on Stash", "Show overlay on maps in stash tabs")]
    public ToggleNode ShowOnStash { get; set; } = new ToggleNode(true);

    // Color thresholds in chaos
    [Menu("Good Threshold (chaos)", "Maps above this value get a green highlight")]
    public RangeNode<int> GoodThreshold { get; set; } = new RangeNode<int>(200, 10, 5000);

    [Menu("Great Threshold (chaos)", "Maps above this value get a gold highlight")]
    public RangeNode<int> GreatThreshold { get; set; } = new RangeNode<int>(500, 50, 10000);
}
