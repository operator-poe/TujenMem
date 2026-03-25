using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace TujenMem.MapRoller;

[Submenu(CollapsedByDefault = true)]
public class MapRollerSettings
{
    [Menu("Enable Map Rolling", "Roll maps in inventory when the hotkey is pressed")]
    public ToggleNode EnableRolling { get; set; } = new ToggleNode(false);

    [Menu("Min Quantity", "Minimum quantity to accept on a map")]
    public RangeNode<int> MinQuantity { get; set; } = new RangeNode<int>(60, 1, 200);

    [Menu("Mods Blacklist", "Comma-separated list of mod substrings to avoid")]
    public TextNode ModsBlackList { get; set; } = new TextNode("reflect");

    [Menu("Use Chaos", "Use Chaos Orb spam (true) or Alch/Scour batching (false)")]
    public ToggleNode UseChaos { get; set; } = new ToggleNode(true);

    [Menu("Enable Exalting", "After rolling good mods, apply Exalted Orbs to fill empty mod slots")]
    public ToggleNode EnableExalting { get; set; } = new ToggleNode(false);

    [Menu("Show Item Overlay", "Show green/yellow/red overlay on maps in inventory")]
    public ToggleNode ShowItemOverlay { get; set; } = new ToggleNode(false);
}
