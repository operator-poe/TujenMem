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
