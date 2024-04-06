using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;

namespace TujenMem;

public class CustomPrice
{
    public string Name { get; set; }
    public float? Value { get; set; } = null;

    public string Expression { get; set; } = "";

    public CustomPrice(string name, float value)
    {
        Name = name;
        Value = value;
    }

    public CustomPrice(string name, string expression)
    {
        Name = name;
        Expression = expression;
    }
}

public class TujenMemSettings : ISettings
{

    public static void HelpMarker(string desc)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    // Special types
    public List<string> Blacklist { get; set; } = new List<string>();

    public List<string> Whitelist { get; set; } = new List<string>();

    public List<(string, float?, string)> CustomPrices { get; set; } = new List<(string, float?, string)>();

    public List<(List<string>, string)> ItemMappings { get; set; } = new List<(List<string>, string)>();


    public TujenMemSettings()
    {
        var whiteListInput = "";
        var whiteListSelected = "";
        WhitelistNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Whitelist"))
                {
                    ImGui.InputTextWithHint("##WhitelistInput", "Keyword", ref whiteListInput, 100);
                    ImGui.SameLine();
                    if (ImGui.Button("Add to Whitelist"))
                    {
                        Whitelist.Add(whiteListInput);
                        whiteListInput = "";
                    }

                    ImGui.BeginChild("##WhitelistList", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Border);
                    foreach (var s in Whitelist)
                    {
                        if (ImGui.Selectable(s, s == whiteListSelected))
                        {
                            whiteListSelected = s;
                        }
                    }
                    ImGui.EndChild();
                    if (whiteListSelected != "")
                    {
                        ImGui.Text("Selected: " + whiteListSelected);
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            Whitelist.Remove(whiteListSelected);
                            whiteListSelected = "";
                        }
                    }
                    ImGui.TreePop();
                }
            }
        };
        var blackListInput = "";
        var blackListSelected = "";
        BlackListNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Blacklist"))
                {
                    ImGui.InputTextWithHint("##BlacklistInput", "Keyword", ref blackListInput, 100);
                    ImGui.SameLine();
                    if (ImGui.Button("Add to Blacklist"))
                    {
                        Blacklist.Add(blackListInput);
                        blackListInput = "";
                    }

                    ImGui.BeginChild("##BlackListList", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Border);
                    foreach (var s in Blacklist)
                    {
                        if (ImGui.Selectable(s, s == blackListSelected))
                        {
                            blackListSelected = s;
                        }
                    }
                    ImGui.EndChild();
                    if (blackListSelected != "")
                    {
                        ImGui.Text("Selected: " + blackListSelected);
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            Blacklist.Remove(blackListSelected);
                            blackListSelected = "";
                        }
                    }
                    ImGui.TreePop();
                }
            }
        };


        (string, float?, string) customPriceSelected = ("", null, "");
        var customPriceInput = "";
        var customPriceValue = 0f;
        var customPriceExpression = "";
        CustomPricesNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Custom Prices"))
                {
                    ImGui.InputText("##CustomPriceName", ref customPriceInput, 100);
                    ImGui.SameLine();
                    ImGui.Text("Item Name");
                    ImGui.InputFloat("##CustomPriceValue", ref customPriceValue);
                    ImGui.SameLine();
                    ImGui.Text("Item Price");
                    ImGui.InputTextWithHint("##CustomPriceExpression", "Expression", ref customPriceExpression, 100);
                    ImGui.SameLine();
                    ImGui.Text("(or) Expression");
                    if (ImGui.Button("Add to List"))
                    {
                        CustomPrices.Add((customPriceInput, customPriceValue > 0f ? customPriceValue : null, customPriceExpression));
                        customPriceInput = "";
                        customPriceValue = 0f;
                        customPriceExpression = "";
                        Ninja.SetDirty();
                    }

                    ImGui.BeginChild("##CustomPricesList", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Border);
                    foreach (var s in CustomPrices)
                    {
                        var t = s.Item2 != null ? s.Item2.ToString() : s.Item3;
                        if (ImGui.Selectable($"{s.Item1} - {t}", s.Item1 == customPriceSelected.Item1))
                        {
                            customPriceSelected = s;
                        }
                    }
                    ImGui.EndChild();
                    if (customPriceSelected.Item1 != "")
                    {
                        var t = customPriceSelected.Item2 != null ? customPriceSelected.Item2.ToString() : customPriceSelected.Item3;
                        ImGui.Text($"Selected: {customPriceSelected.Item1} - {t}");
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            CustomPrices.Remove(customPriceSelected);
                            customPriceSelected = ("", null, "");
                            Ninja.SetDirty();
                        }
                    }

                    ImGui.TreePop();
                }
            }
        };

        (List<string>, string) itemMappingSelected = (new List<string>(), "");
        var itemMappingInput = "";
        var itemMappingValue = "";
        ItemMappingsNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Item Mappings"))
                {
                    ImGui.InputText("##ItemMappingName", ref itemMappingInput, 100);
                    ImGui.SameLine();
                    ImGui.Text("Item Name");
                    ImGui.InputText("##ItemMappingValue", ref itemMappingValue, 100);
                    ImGui.SameLine();
                    ImGui.Text("Mapped Name");
                    if (ImGui.Button("Add to Mapping"))
                    {
                        ItemMappings.Add((new List<string>(itemMappingInput.Split(',')), itemMappingValue));
                        itemMappingInput = "";
                        itemMappingValue = "";
                        Ninja.SetDirty();
                    }
                    var t2 = string.Join(",", itemMappingSelected.Item1) + itemMappingSelected.Item2;

                    ImGui.BeginChild("##ItemMappingsList", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Border);
                    foreach (var s in ItemMappings)
                    {
                        var t1 = string.Join(",", s.Item1) + s.Item2;
                        if (ImGui.Selectable($"{string.Join(",", s.Item1)} - {s.Item2}", t1 == t2))
                        {
                            itemMappingSelected = s;
                        }
                    }
                    ImGui.EndChild();
                    if (t2 != "")
                    {
                        ImGui.Text($"Selected: {string.Join(",", itemMappingSelected.Item1)} - {itemMappingSelected.Item2}");
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            ItemMappings.Remove(itemMappingSelected);
                            itemMappingSelected = (new List<string>(), "");
                            Ninja.SetDirty();
                        }
                    }

                    ImGui.TreePop();
                }
            }
        };
        FetchNinja = new CustomNode
        {
            DrawDelegate = () =>
            {
                Ninja.RenderSettings();
            }
        };
    }

    public void SetDefaults()
    {
        if (ItemMappings.Count == 0)
        {
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Mansion" }, "Enchanted Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Records Office" }, "Enchanted Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Bunker" }, "Enchanted Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Smuggler's Den" }, "Trinkets Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Underbelly" }, "Trinkets Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Laboratory" }, "Replicas Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Prohibited Library" }, "Replicas Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistBlueprint", "Tunnels" }, "Unusual Blueprint"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Deception" }, "Good Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Perception" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Agility" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Lockpicking" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Demolition" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Engineering" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Trap Disarmament" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Brute Force" }, "Bad Contract"));
            ItemMappings.Add((new List<string>() { "HeistContract", "Counter-Thaumaturgy" }, "Bad Contract"));
        }

        if (CustomPrices.Count == 0)
        {
            CustomPrices.Add(("Chaos Orb", 1, ""));
            CustomPrices.Add(("Good Contract", 10, ""));
            CustomPrices.Add(("Bad Contract", 1, ""));
            CustomPrices.Add(("Enchanted Blueprint", 3, ""));
            CustomPrices.Add(("Trinkets Blueprint", 2, ""));
            CustomPrices.Add(("Replicas Blueprint", 15, ""));
            CustomPrices.Add(("Unusual Blueprint", 15, ""));
            CustomPrices.Add(("Rogue's Marker", 0.004f, ""));
            CustomPrices.Add(("Influenced Map", 15, ""));
            CustomPrices.Add(("Occupied Map", 15, ""));
            CustomPrices.Add(("Ritual Splinter", null, "{Ritual Vessel}/100"));
            CustomPrices.Add(("Annulment Shard", null, "{Orb of Annulment}/20"));
            CustomPrices.Add(("Ancient Orb", 6, ""));
            CustomPrices.Add(("Ancient Shard", null, "{Ancient Orb}/20"));
            CustomPrices.Add(("Harbinger's Shard", null, "{Harbinger's Orb}/20"));
        }

        if (Blacklist.Count == 0)
        {
            Blacklist.Add("Whetstone");
            Blacklist.Add("AbyssJewel");
            Blacklist.Add("Scrap");
            Blacklist.Add("Transmutation");
            Blacklist.Add("Orb of Binding");
            Blacklist.Add("Breach Ring");
            Blacklist.Add("Sacrifice at");
            Blacklist.Add("Clear Oil");
            Blacklist.Add("Sepia Oil");
            Blacklist.Add("Amber Oil");
            Blacklist.Add("Incubator");
            Blacklist.Add("Whakawairua");
            Blacklist.Add("Blaidd");
            Blacklist.Add("Maelström");
            Blacklist.Add("Mao Kun");
        }

        if (Whitelist.Count == 0)
        {
            Whitelist.Add("Diviner's Incubator");
            Whitelist.Add("Kalguuran Incubator");
            Whitelist.Add("Ornate Incubator");
            Whitelist.Add("Vaal Temple");
        }
    }

    [JsonIgnore]
    public CustomNode FetchNinja { get; set; }
    [Menu("Enable Test Mode", "Will only try to read items from the Haggle inventory. Won't haggle, won't refresh window. Use together with 'Show Debug Window' to see what's going on.")]
    public ToggleNode DebugOnly { get; set; } = new ToggleNode(false);
    public ToggleNode ShowDebugWindow { get; set; } = new ToggleNode(false);
    public ToggleNode ExpeditionMapHelper { get; set; } = new ToggleNode(true);

    public HotKeySettings HotKeySettings { get; set; } = new HotKeySettings();

    public TextNode League { get; set; } = new TextNode("Ancestor");

    [Menu("HoverItem Delay", "Delay used to wait inbetween checks for the Hoveritem (in ms).")]
    public RangeNode<int> HoverItemDelay { get; set; } = new RangeNode<int>(15, 0, 100);
    public ToggleNode EmptyInventoryAfterHaggling { get; set; } = new ToggleNode(false);


    // ---------------------------------------------------------------------------------------------------

    public HaggleMultiplierSettings HaggleMultiplierSettings { get; set; } = new HaggleMultiplierSettings();
    public ArtifactValueSettings ArtifactValueSettings { get; set; } = new ArtifactValueSettings();

    [JsonIgnore]
    public CustomNode WhitelistNode { get; set; }

    [JsonIgnore]
    public CustomNode BlackListNode { get; set; }
    [JsonIgnore]
    public CustomNode CustomPricesNode { get; set; }
    [JsonIgnore]
    public CustomNode ItemMappingsNode { get; set; }

    // ---------------------------------------------------------------------------------------------------

    [Menu("Maps Enabled", "Enable haggling for maps.")]
    public ToggleNode MapsEnabled { get; set; } = new ToggleNode(true);

    [Menu("Min Map Tier", "Minimum map tier to haggle.")]
    public RangeNode<int> MinMapTier { get; set; } = new RangeNode<int>(16, 1, 16);

    [Menu("Unique Maps Always Enabled", "Enable override for unique maps")]
    public ToggleNode UniqueMapsAlwaysEnabled { get; set; } = new ToggleNode(true);

    [Menu("Custom Name For Influenced Maps", "Custom name for influenced maps (Shaper, Elder, Conquerer, etc.) (Leave blank to disable")]
    public TextNode CustomNameForInfluencedMaps { get; set; } = new TextNode("Influenced Map");

    public PrepareLogbookSettings PrepareLogbookSettings { get; set; } = new PrepareLogbookSettings();
    public SillyOrExperimenalFeatures SillyOrExperimenalFeatures { get; set; } = new SillyOrExperimenalFeatures();
    public Gwennen Gwennen { get; set; } = new Gwennen();

    public ListNode LogLevel { get; set; } = new ListNode
    {
        Values = new List<string> { "None", "Debug", "Error" },
        Value = "Error"
    };
}

[Submenu(CollapsedByDefault = true)]
public class HotKeySettings
{
    public HotkeyNode StartHotKey { get; set; } = new HotkeyNode(Keys.F1);
    public HotkeyNode StopHotKey { get; set; } = new HotkeyNode(Keys.Delete);
    public HotkeyNode RollAndBlessHotKey { get; set; } = new HotkeyNode(Keys.F4);
}

[Submenu(CollapsedByDefault = true)]
public class HaggleMultiplierSettings
{

    [Menu("Try 1", "Multiplier for first haggle attempt")]
    public RangeNode<float> Try1 { get; set; } = new RangeNode<float>(0.72f, 0.0f, 1.0f);

    [Menu("Try 2", "Multiplier for second haggle attempt")]
    public RangeNode<float> Try2 { get; set; } = new RangeNode<float>(0.5f, 0.0f, 1.0f);

    [Menu("Try 3", "Multiplier for third haggle attempt")]
    public RangeNode<float> Try3 { get; set; } = new RangeNode<float>(0.7f, 0.0f, 1.0f);

    [Menu("Multiplier Mode", "Wether the multiplier applies as percentage of 0 to max or current tries min to max")]
    public ListNode MultiplierMode { get; set; } = new ListNode
    {
        Values = new List<string> { "Zero To Max", "Min to Max" },
        Value = "Min To Max"
    };
}

[Submenu(CollapsedByDefault = true)]
public class ArtifactValueSettings
{

    [Menu("Enable Lesser", "If disabled, Lesser Black Scythe Artifact will be ignored")]
    public ToggleNode EnableLesser { get; set; } = new ToggleNode(true);
    [Menu("Value Lesser", "Value of Lesser Black Scythe Artifact")]
    public RangeNode<float> ValueLesser { get; set; } = new RangeNode<float>(0.007f, 0, 0.1f);

    [Menu("Enable Greater", "If disabled, Greater Black Scythe Artifact will be ignored")]
    public ToggleNode EnableGreater { get; set; } = new ToggleNode(true);
    [Menu("Value Greater", "Value of Greater Black Scythe Artifact")]
    public RangeNode<float> ValueGreater { get; set; } = new RangeNode<float>(0.018f, 0, 0.1f);

    [Menu("Enable Grand", "If disabled, Grand Black Scythe Artifact will be ignored")]
    public ToggleNode EnableGrand { get; set; } = new ToggleNode(true);
    [Menu("Value Grand", "Value of Grand Black Scythe Artifact")]
    public RangeNode<float> ValueGrand { get; set; } = new RangeNode<float>(0.023f, 0, 0.1f);

    [Menu("Enable Exceptional", "If disabled, Exceptional Black Scythe Artifact will be ignored")]
    public ToggleNode EnableExceptional { get; set; } = new ToggleNode(true);
    [Menu("Value Exceptional", "Value of Exceptional Black Scythe Artifact")]
    public RangeNode<float> ValueExceptional { get; set; } = new RangeNode<float>(0.060f, 0, 0.1f);
    [Menu("Buy items when value is > x% of it's price")]
    public RangeNode<float> ItemPriceMultiplier { get; set; } = new RangeNode<float>(0.7f, 0.0f, 1.0f);
}


[Submenu(CollapsedByDefault = true)]
public class PrepareLogbookSettings
{
    public List<string> FactionOrder { get; set; } = new List<string>();
    public List<string> AreaOrder { get; set; } = new List<string>();
    public PrepareLogbookSettings()
    {
        FactionOrderNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Faction Order"))
                {
                    ImGui.SameLine();
                    TujenMemSettings.HelpMarker("Order of factions to prioritize when rolling logbooks. (Drag and drop to reorder)");
                    for (int n = 0; n < FactionOrder.Count; n++)
                    {
                        string item = FactionOrder[n];
                        ImGui.Selectable(item);

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                        {
                            int nNext = n + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                            if (nNext >= 0 && nNext < FactionOrder.Count)
                            {
                                FactionOrder[n] = FactionOrder[nNext];
                                FactionOrder[nNext] = item;
                                ImGui.ResetMouseDragDelta();
                            }
                        }

                    }
                    ImGui.Separator();
                    ImGui.TreePop();
                }
                else
                {
                    ImGui.SameLine();
                    TujenMemSettings.HelpMarker("Order of factions to prioritize when rolling logbooks. (Drag and drop to reorder)");
                }
            }
        };
        AreaOrderNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("Area Order"))
                {
                    ImGui.SameLine();
                    TujenMemSettings.HelpMarker("Order of areas to prioritize when rolling logbooks. (Drag and drop to reorder)");
                    for (int n = 0; n < AreaOrder.Count; n++)
                    {
                        string item = AreaOrder[n];
                        ImGui.Selectable(item);

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                        {
                            int nNext = n + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                            if (nNext >= 0 && nNext < AreaOrder.Count)
                            {
                                AreaOrder[n] = AreaOrder[nNext];
                                AreaOrder[nNext] = item;
                                ImGui.ResetMouseDragDelta();
                            }
                        }

                    }
                    ImGui.Separator();
                    ImGui.TreePop();
                }
                else
                {
                    ImGui.SameLine();
                    TujenMemSettings.HelpMarker("Order of areas to prioritize when rolling logbooks. (Drag and drop to reorder)");
                }
            }
        };
    }

    public void SetDefaults()
    {
        if (FactionOrder.Count == 0)
        {
            FactionOrder = new List<string>{
                "Knights of the Sun",
                "Black Scythe Mercenaries",
                "Druids of the Broken Circle",
                "Order of the Chalice"
            };
        }
        if (AreaOrder.Count == 0)
        {
            AreaOrder = new List<string>{
                "Dried Riverbed",
                "Volcanic Island",
                "Karui Wargraves",
                "Battleground Graves",
                "Bluffs",
                "Desert Ruins",
                "Mountainside",
                "Shipwreck Reef",
                "Karui Wargraves",
                "Scrublands",
                "Vaal Temple",
                "Cemetery",
                "Forest Ruins",
                "Utzaal Outskirts",
                "Sarn Slums",
                "Rotting Temple"
            };
        }
    }

    public ToggleNode EnableRolling { get; set; } = new ToggleNode(true);
    public ToggleNode EnableBlessing { get; set; } = new ToggleNode(true);
    [JsonIgnore]
    public CustomNode FactionOrderNode { get; set; }
    [JsonIgnore]
    public CustomNode AreaOrderNode { get; set; }

    public RangeNode<int> MinQuantity { get; set; } = new RangeNode<int>(60, 1, 120);
    public TextNode ModsBlackList { get; set; } = new TextNode("regenerate");
    public ToggleNode UseChaos { get; set; } = new ToggleNode(true);

    public ToggleNode Bless_ExplosiveRange { get; set; } = new ToggleNode(true);
    public RangeNode<int> Bless_ExplosiveRange_Min { get; set; } = new RangeNode<int>(40, 20, 50);

    public ToggleNode Bless_NumberOfExplosives { get; set; } = new ToggleNode(true);
    public RangeNode<int> Bless_NumberOfExplosives_Min { get; set; } = new RangeNode<int>(30, 10, 40);

    public ToggleNode Bless_ExplosiveRadius { get; set; } = new ToggleNode(true);
    public RangeNode<int> Bless_ExplosiveRadius_Min { get; set; } = new RangeNode<int>(30, 10, 40);

    public ToggleNode Bless_AdditionalChests { get; set; } = new ToggleNode(true);
    public RangeNode<int> Bless_AdditionalChests_Min { get; set; } = new RangeNode<int>(12, 8, 16);

    public ToggleNode Bless_QuantityOfArtifactsByMonsters { get; set; } = new ToggleNode(true);
    public RangeNode<int> Bless_QuantityOfArtifactsByMonsters_Min { get; set; } = new RangeNode<int>(25, 10, 40);

    public ToggleNode Bless_NumberOfMonsterMarkers { get; set; } = new ToggleNode(false);
    public RangeNode<int> Bless_NumberOfMonsterMarkers_Min { get; set; } = new RangeNode<int>(30, 10, 40);

    public ToggleNode Bless_NumberOfRemnants { get; set; } = new ToggleNode(false);
    public RangeNode<int> Bless_NumberOfRemnants_Min { get; set; } = new RangeNode<int>(30, 10, 40);

    public ToggleNode Bless_AdditionalSuffixMod { get; set; } = new ToggleNode(false);
    public RangeNode<int> Bless_AdditionalSuffixMod_Min { get; set; } = new RangeNode<int>(30, 10, 40);
}


[Submenu(CollapsedByDefault = true)]
public class SillyOrExperimenalFeatures
{
    [Menu("Enable Buy Assistance", "You probably don't want to use this.")]
    public ToggleNode EnableBuyAssistance { get; set; } = new ToggleNode(false);
    [Menu("Enable Statistics", "Output at: Plugins/(Temp or Compiled)/Data/Statistics.csv")]
    public ToggleNode EnableStatistics { get; set; } = new ToggleNode(false);

    [Menu("Enable Vorana Warning", "THIS IS REALLY SILLY. DO NOT ENABLE THIS UNLESS YOU WANT TO BE ANNOYED.")]
    public ToggleNode EnableVoranaWarning { get; set; } = new ToggleNode(false);
    [Menu("Enable Vorana Warning Sound", "THIS IS REALLY SILLY. DO NOT ENABLE THIS UNLESS YOU WANT TO BE ANNOYED.")]
    public ToggleNode EnableVoranaWarningSound { get; set; } = new ToggleNode(false);
}


[Submenu(CollapsedByDefault = true)]
public class Gwennen
{
    public ToggleNode EnableGwennen { get; set; } = new ToggleNode(false);

    public List<string> BaseList { get; set; } = new List<string>();

    [JsonIgnore]
    public CustomNode BaseListNode { get; set; }

    public Gwennen()
    {
        var baseListInput = "";
        var baseListSelected = "";
        BaseListNode = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.TreeNode("BaseList"))
                {
                    ImGui.InputTextWithHint("##BaseInput", "Keyword", ref baseListInput, 100);
                    ImGui.SameLine();
                    if (ImGui.Button("Add to Blacklist"))
                    {
                        BaseList.Add(baseListInput);
                        baseListInput = "";
                    }

                    ImGui.BeginChild("##BaseListList", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.Border);
                    foreach (var s in BaseList)
                    {
                        if (ImGui.Selectable(s, s == baseListSelected))
                        {
                            baseListSelected = s;
                        }
                    }
                    ImGui.EndChild();
                    if (baseListSelected != "")
                    {
                        ImGui.Text("Selected: " + baseListSelected);
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            BaseList.Remove(baseListSelected);
                            baseListSelected = "";
                        }
                    }
                    ImGui.TreePop();
                }
            }
        };
    }
}