using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Cache;
using ImGuiNET;
using SharpDX;

namespace TujenMem.ItemOverlay;

public class MirageWishesOverlay : IInventoryOverlayRenderer
{
    private class MirageWishEntry
    {
        public string Wish;
        public string Description;
        public string Keyword;
        public string Aspect;
        public int DefaultWeight;
    }

    private readonly CachedValue<List<OverlayDrawItem>> _cache;
    private readonly Dictionary<string, List<MirageWishEntry>> _wishLookup;

    private const uint ColorFillGreen = 0x4000FF00;
    private const uint ColorBorderGreen = 0xFF00FF00;
    private const uint ColorFillYellow = 0x4000FFFF;
    private const uint ColorBorderYellow = 0xFF00FFFF;

    private static readonly List<MirageWishEntry> AllWishes = new()
    {
        new() { Wish = "Wish for Fishes", Description = "Claim a Fishing Rod.", Aspect = null, DefaultWeight = 10 },
        new() { Wish = "Wish for Foes", Description = "Rare Monsters in the Mirage Area will have two additional modifiers.", Aspect = "Skill", DefaultWeight = 5 },
        new() { Wish = "Wish for Rebirth", Description = "Monsters in the Mirage Area will have a chance to revive when slain.", Aspect = null, DefaultWeight = 4 },
        new() { Wish = "Wish for Troves", Description = "An additional Unique Strongbox will appear in the Mirage Area.", Aspect = "Skill", DefaultWeight = 4 },
        new() { Wish = "Wish for Glittering", Description = "Skill and Support Gems found in Mirage Area will have a random amount of Quality.", Aspect = "Power", DefaultWeight = 1 },
        new() { Wish = "Wish for Wealth", Description = "100% more Currency found in the Mirage Area.", Aspect = "Skill", DefaultWeight = 7 },
        new() { Wish = "Wish for Foreknowledge", Description = "100% more Divination Cards found in the Mirage Area.", Aspect = "Knowledge", DefaultWeight = 1 },
        new() { Wish = "Wish for Scarabs", Description = "80% more Scarabs found in the Mirage Area.", Aspect = "Power", DefaultWeight = 6 },
        new() { Wish = "Wish for Horizons", Description = "100% more Maps found in the Mirage Area.", Aspect = "Knowledge", DefaultWeight = 8 },
        new() { Wish = "Wish for Treasures", Description = "80% increased Rarity of items found in the Mirage Area.", Aspect = "Skill", DefaultWeight = 4 },
        new() { Wish = "Wish for Hordes", Description = "20% increased Pack Size in the Mirage Area.", Aspect = "Knowledge", DefaultWeight = 6 },
        new() { Wish = "Wish for Gold", Description = "Players in Mirage Area find 80% more Gold from slain Enemies.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Titans", Description = "Mirage Area will contain an additional pack of Atlas Bosses.", Keyword = "an additional pack", Aspect = "Knowledge", DefaultWeight = 8 },
        new() { Wish = "Wish for Titans", Description = "Mirage Area will contain 3 additional packs of Atlas Bosses.", Keyword = "3 additional packs", Aspect = "Knowledge", DefaultWeight = 9 },
        new() { Wish = "Wish for Jewels", Description = "An additional Bronze Jewel Cache will appear in the Mirage Area.", Keyword = "Bronze", Aspect = "Skill", DefaultWeight = 1 },
        new() { Wish = "Wish for Jewels", Description = "An additional Silver Jewel Cache will appear in the Mirage Area.", Keyword = "Silver", Aspect = "Skill", DefaultWeight = 1 },
        new() { Wish = "Wish for Jewels", Description = "An additional Golden Jewel Cache will appear in the Mirage Area.", Keyword = "Golden", Aspect = "Skill", DefaultWeight = 6 },
        new() { Wish = "Wish for Meddling", Description = "Mirage Area will contain 12 additional packs of Astral monsters.", Aspect = "Knowledge", DefaultWeight = 6 },
        new() { Wish = "Wish for Risk", Description = "Mirage Area will contain 12 additional packs of difficult and rewarding monsters.", Aspect = "Power", DefaultWeight = 6 },
        new() { Wish = "Wish for Prosperity", Description = "Mirage Area will contain an additional fountain of wealth.", Aspect = "Knowledge", DefaultWeight = 3 },
        new() { Wish = "Wish for Uncertainty", Description = "Mirage Area will be affected by 10 random Scarab modifiers.", Aspect = null, DefaultWeight = 8 },
        new() { Wish = "Wish for Hindrance", Description = "Enemies in the Mirage Area will be Chilled and Hindered .", Aspect = "Knowledge", DefaultWeight = 2 },
        new() { Wish = "Wish for Pursuit", Description = "Enemies in the Mirage Area have a 4% chance to release a Golden Volatile on death.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Oases", Description = "Mirage Area has patches of Oasis Ground.", Aspect = "Knowledge", DefaultWeight = 2 },
        new() { Wish = "Wish for Rust", Description = "Breaking the Astral Chain in the Mirage Area will spawn Ridan, of the Afarud .", Aspect = "Skill", DefaultWeight = 12 },
        new() { Wish = "Wish for Providence", Description = "The Nameless Seer will appear on breaking the Astral Chain in the Mirage Area", Aspect = "Knowledge", DefaultWeight = 10 },
        new() { Wish = "Wish for Reflection", Description = "A Reflecting Mist will appear on breaking the Astral Chain in the Mirage Area", Aspect = "Knowledge", DefaultWeight = 11 },
        new() { Wish = "Wish for Godhood", Description = "Players in Mirage Area have Echoing Shrine and Divine Shrine.", Aspect = "Knowledge", DefaultWeight = 3 },
        new() { Wish = "Wish for Knowledge", Description = "Players in Mirage Area gain 50% increased Experience.", Aspect = "Skill", DefaultWeight = 1 },
        new() { Wish = "Wish for Power", Description = "Enemies slain by Players in Mirage Area explode on death.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Momentum", Description = "Players in Mirage Area have Onslaught and Adrenaline.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Souls", Description = "Players in Mirage Area have Soul Eater.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Elements", Description = "Players in Mirage Area have the Blessing of the Storm.", Aspect = "Skill", DefaultWeight = 2 },
        new() { Wish = "Wish for Wisps", Description = "Enemies in Mirage Area have a chance to be empowered by Wildwood Wisps.", Aspect = "Knowledge", DefaultWeight = 4 },
        new() { Wish = "Wish for Croaks", Description = "Mirage Area contains additional frogs.", Aspect = null, DefaultWeight = 5 },
        new() { Wish = "Wish for Glyphs", Description = "Portal Scrolls and Wisdom Scrolls found in Mirage Area will instead drop as other Currencies.", Aspect = "Power", DefaultWeight = 7 },
        new() { Wish = "Wish for Trinkets", Description = "Jewellery found in Mirage Area will instead drop as Jewels.", Keyword = "instead drop", Aspect = "Power", DefaultWeight = 1 },
        new() { Wish = "Wish for Terror", Description = "Map Boss of the Mirage Area will be accompanied by a Pinnacle Atlas Boss from The Feared.", Aspect = null, DefaultWeight = 9 },
        new() { Wish = "Wish for Eminence", Description = "Breaking the Astral Chain in the Mirage Area will reward an additional Unique Jewel.", Aspect = null, DefaultWeight = 8 },
        new() { Wish = "Wish for Avarice", Description = "Some packs in the Mirage Area will be replaced with Monsters that convert dropped Equipment to Gold.", Aspect = "Power", DefaultWeight = 3 },
        new() { Wish = "Wish for Betrayal", Description = "Some packs in the Mirage Area will be replaced with Syndicate members.", Aspect = "Skill", DefaultWeight = 8 },
        new() { Wish = "Wish for Phantoms", Description = "Some packs in the Mirage Area will be replaced with Monsters that cannot drop Equipment.", Aspect = "Knowledge", DefaultWeight = 5 },
        new() { Wish = "Wish for Fortune", Description = "Breaking the Astral Chain in the Mirage Area will reward a cache of Currency.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Skittering", Description = "Breaking the Astral Chain in the Mirage Area will reward a cache of Scarabs.", Aspect = "Skill", DefaultWeight = 3 },
        new() { Wish = "Wish for Augury", Description = "Breaking the Astral Chain in the Mirage Area will reward a cache of Stacked Decks.", Aspect = "Knowledge", DefaultWeight = 2 },
        new() { Wish = "Wish for Distant Horizons", Description = "Breaking the Astral Chain in the Mirage Area will reward a cache of Maps.", Aspect = "Power", DefaultWeight = 2 },
        new() { Wish = "Wish for Strange Horizons", Description = "Breaking the Astral Chain in the Mirage Area will reward a Unique Map.", Aspect = "Skill", DefaultWeight = 5 },
        new() { Wish = "Wish for Flame", Description = "Breaking the Astral Chain in the Mirage Area will reward a Coin of Power.", Aspect = "Power", DefaultWeight = 11 },
        new() { Wish = "Wish for Tides", Description = "Breaking the Astral Chain in the Mirage Area will reward a Coin of Knowledge.", Aspect = "Knowledge", DefaultWeight = 10 },
        new() { Wish = "Wish for Sands", Description = "Breaking the Astral Chain in the Mirage Area will reward a Coin of Skill.", Aspect = "Skill", DefaultWeight = 12 },
        new() { Wish = "Wish for Binding", Description = "Breaking the Astral Chain in the Mirage Area will reward Orbs of Binding.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Regency", Description = "Breaking the Astral Chain in the Mirage Area will reward Regal Orbs.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Connections", Description = "Breaking the Astral Chain in the Mirage Area will reward Fusing or Jewellers Orbs.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Ancient Protection", Description = "Breaking the Astral Chain in the Mirage Area will reward a Unique Armour.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Ancient Armaments", Description = "Breaking the Astral Chain in the Mirage Area will reward a Unique Weapon.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Ancient Curios", Description = "Breaking the Astral Chain in the Mirage Area will reward a Unique Jewellery item.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Craftsmanship", Description = "Breaking the Astral Chain in the Mirage Area will reward a Five-Linked Body Armour.", Aspect = "Knowledge", DefaultWeight = 0 },
        new() { Wish = "Wish for Mosaics", Description = "Breaking the Astral Chain in the Mirage Area will reward Chromatic Orbs.", Aspect = null, DefaultWeight = 1 },
        new() { Wish = "Wish for Swiftness", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Boots.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Helms", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Helmets.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Mitts", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Gloves.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Protection", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Body Armours.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Blades", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Melee Weapons.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Missiles", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Ranged Weapons.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Bastions", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Shields.", Aspect = null, DefaultWeight = 0 },
        new() { Wish = "Wish for Trinkets", Description = "Breaking the Astral Chain in the Mirage Area will reward Rare Jewellery.", Keyword = "reward Rare Jewellery", Aspect = null, DefaultWeight = 0 },
    };

    public MirageWishesOverlay()
    {
        _wishLookup = InitWishData();
        _cache = new TimeCache<List<OverlayDrawItem>>(BuildItems, 500);
    }

    private static Dictionary<string, List<MirageWishEntry>> InitWishData()
    {
        var grouped = AllWishes
            .GroupBy(e => (e.Wish ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var settings = TujenMem.Instance.Settings.MirageWishesSettings;
        settings.WishWeights ??= new();

        var displayList = new List<MirageWishesSettings.WishDisplayInfo>();
        foreach (var entry in AllWishes.OrderBy(e => e.Wish, StringComparer.OrdinalIgnoreCase))
        {
            bool isDuplicate = grouped.TryGetValue((entry.Wish ?? "").Trim(), out var group) && group.Count > 1;
            var label = isDuplicate && !string.IsNullOrEmpty(entry.Keyword)
                ? $"{entry.Wish} ({entry.Keyword})##{entry.Description}"
                : $"{entry.Wish}##{entry.Description}";

            var key = entry.Description?.Trim() ?? "";
            displayList.Add(new MirageWishesSettings.WishDisplayInfo { Key = key, Label = label });

            if (!settings.WishWeights.ContainsKey(key))
                settings.WishWeights[key] = entry.DefaultWeight;
        }
        settings.WishDisplay = displayList;

        return grouped;
    }

    private static int AspectRank(string aspect) => aspect switch
    {
        "Skill" => 3,
        "Power" => 2,
        "Knowledge" => 1,
        _ => 0,
    };

    public bool ShouldRender()
    {
        return TujenMem.Instance.Settings.MirageWishesSettings.EnableOverlay &&
            TujenMem.Instance.GameController.IngameState.IngameUi
                .MirageWishesPanel is { IsVisible: true };
    }

    private static int GetWeight(MirageWishEntry entry)
    {
        var key = entry.Description?.Trim() ?? "";
        var weights = TujenMem.Instance.Settings.MirageWishesSettings.WishWeights;
        return weights != null && weights.TryGetValue(key, out var w) ? w : 0;
    }

    private (int Weight, string Aspect) LookupWishInfo(string wishName, string description)
    {
        if (string.IsNullOrEmpty(wishName)) return (0, null);
        if (!_wishLookup.TryGetValue(wishName.Trim(), out var candidates))
            return (0, null);

        MirageWishEntry matched;
        if (candidates.Count == 1)
        {
            matched = candidates[0];
        }
        else if (!string.IsNullOrEmpty(description))
        {
            matched = candidates.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Keyword) &&
                description.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase))
                ?? candidates[0];
        }
        else
        {
            matched = candidates.OrderByDescending(GetWeight).First();
        }

        return (GetWeight(matched), matched.Aspect);
    }

    private List<OverlayDrawItem> BuildItems()
    {
        var items = new List<OverlayDrawItem>();

        var miragePanel = TujenMem.Instance.GameController.IngameState.IngameUi.MirageWishesPanel;
        if (miragePanel is not { IsVisible: true })
            return items;

        var panels = new[]
        {
            miragePanel.GetChildFromIndices(4, 3),
            miragePanel.GetChildFromIndices(4, 4),
            miragePanel.GetChildFromIndices(4, 5),
        };

        var weights = new int[3];
        var aspects = new string[3];
        for (int i = 0; i < 3; i++)
        {
            if (panels[i] == null) return items;
            var wishName = panels[i].GetChildAtIndex(2)?.Text;
            if (string.IsNullOrEmpty(wishName)) return items;

            string description = null;
            if (_wishLookup.TryGetValue(wishName.Trim(), out var candidates) && candidates.Count > 1)
                description = panels[i].GetChildAtIndex(3)?.Text;

            (weights[i], aspects[i]) = LookupWishInfo(wishName, description);
        }

        var maxWeight = weights.Max();
        var tiedIndices = Enumerable.Range(0, 3).Where(i => weights[i] == maxWeight).ToList();

        if (tiedIndices.Count == 1)
        {
            AddHighlight(items, panels[tiedIndices[0]].GetClientRect(), isGreen: true);
        }
        else
        {
            var maxAspectRank = tiedIndices.Max(i => AspectRank(aspects[i]));
            var aspectWinners = tiedIndices.Where(i => AspectRank(aspects[i]) == maxAspectRank).ToList();
            bool aspectBroken = aspectWinners.Count == 1;

            foreach (var i in tiedIndices)
                AddHighlight(items, panels[i].GetClientRect(), isGreen: aspectBroken && i == aspectWinners[0]);
        }

        for (int i = 0; i < 3; i++)
        {
            var rect = panels[i].GetClientRect();
            items.Add(new OverlayDrawItem
            {
                TopLeft = new System.Numerics.Vector2(rect.X, rect.Y),
                BottomRight = new System.Numerics.Vector2(rect.X + rect.Width, rect.Y + rect.Height),
                Text = weights[i].ToString(),
                TextColor = 0xFFFFFFFF,
            });
        }

        return items;
    }

    private static void AddHighlight(List<OverlayDrawItem> items, RectangleF rect, bool isGreen)
    {
        items.Add(new OverlayDrawItem
        {
            TopLeft = new System.Numerics.Vector2(rect.X, rect.Y),
            BottomRight = new System.Numerics.Vector2(rect.X + rect.Width, rect.Y + rect.Height),
            FillColor = isGreen ? ColorFillGreen : ColorFillYellow,
            BorderColor = isGreen ? ColorBorderGreen : ColorBorderYellow,
        });
    }

    public void Render(ImDrawListPtr drawList)
    {
        var font = ImGui.GetFont();
        var bigFontSize = font.FontSize * 4f;

        foreach (var item in _cache.Value)
        {
            if (item.FillColor != 0)
                drawList.AddRectFilled(item.TopLeft, item.BottomRight, item.FillColor);
            if (item.BorderColor != 0)
                drawList.AddRect(item.TopLeft, item.BottomRight, item.BorderColor, 0, ImDrawFlags.None, 2);
            if (item.Text != null)
            {
                var textSize = font.CalcTextSizeA(bigFontSize, float.MaxValue, 0, item.Text);
                var centerX = (item.TopLeft.X + item.BottomRight.X - textSize.X) / 2f;
                var pos = new System.Numerics.Vector2(centerX, item.TopLeft.Y + 6);
                const float pad = 6f;
                var bgTL = new System.Numerics.Vector2(pos.X - pad, pos.Y - pad);
                var bgBR = new System.Numerics.Vector2(pos.X + textSize.X + pad, pos.Y + textSize.Y + pad);
                drawList.AddRectFilled(bgTL, bgBR, 0xEE000000, 4f);
                drawList.AddText(font, bigFontSize, pos, item.TextColor, item.Text);
            }
        }
    }
}
