using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;

namespace TujenMem;

[Submenu(CollapsedByDefault = true)]
public class MirageWishesSettings
{
    public struct WishDisplayInfo
    {
        public string Key;
        public string Label;
    }

    [Menu("Enable Overlay", "Highlight the best Mirage Wish option")]
    public ToggleNode EnableOverlay { get; set; } = new ToggleNode(true);

    public Dictionary<string, int> WishWeights { get; set; } = new();

    [JsonIgnore]
    internal List<WishDisplayInfo> WishDisplay { get; set; } = new();

    [JsonIgnore]
    public CustomNode WishWeightsNode { get; set; }

    public MirageWishesSettings()
    {
        WishWeightsNode = new CustomNode
        {
            DrawDelegate = DrawWishWeights
        };
    }

    private void DrawWishWeights()
    {
        if (WishDisplay.Count == 0) return;
        if (!ImGui.TreeNode("Wish Weights")) return;

        WishWeights ??= new();
        foreach (var wish in WishDisplay)
        {
            if (!WishWeights.TryGetValue(wish.Key, out var val))
                val = 0;
            if (ImGui.SliderInt(wish.Label, ref val, 0, 20))
                WishWeights[wish.Key] = val;
        }

        ImGui.TreePop();
    }
}
