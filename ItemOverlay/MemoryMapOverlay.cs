using System;
using System.Collections.Generic;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class MemoryMapOverlay : IInventoryOverlayRenderer
{
    private readonly CachedValue<List<OverlayDrawItem>> _cache;

    // ARGB color constants
    private const uint ColorBgDark = 0xCC000000; // semi-transparent black background
    private const uint ColorWhite = 0xFFFFFFFF;
    private const uint ColorGrey = 0xFFAAAAAA;
    private const uint ColorGood = 0xFF00DD00; // green text
    private const uint ColorGreat = 0xFFFFCC00; // gold text
    private const uint ColorFillLow = 0x20FFFFFF; // very faint white
    private const uint ColorFillGood = 0x2800FF00; // faint green
    private const uint ColorFillGreat = 0x30FFCC00; // faint gold
    private const uint ColorBorderGood = 0xFF00AA00;
    private const uint ColorBorderGreat = 0xFFDDAA00;

    public MemoryMapOverlay()
    {
        _cache = new TimeCache<List<OverlayDrawItem>>(BuildItems, 500);
    }

    public bool ShouldRender()
    {
        var settings = TujenMem.Instance.Settings.MemoryMapPriceSettings;
        if (!settings.EnableOverlay) return false;

        var ui = TujenMem.Instance.GameController.IngameState.IngameUi;
        bool inventoryVisible = settings.ShowOnInventory &&
            ui.InventoryPanel[InventoryIndex.PlayerInventory] is { IsVisible: true };
        bool stashVisible = settings.ShowOnStash &&
            ui.StashElement is { IsVisible: true };

        return inventoryVisible || stashVisible;
    }

    public void Render(ImDrawListPtr drawList)
    {
        foreach (var item in _cache.Value)
        {
            if (item.FillColor != 0)
                drawList.AddRectFilled(item.TopLeft, item.BottomRight, item.FillColor);
            if (item.BorderColor != 0)
                drawList.AddRect(item.TopLeft, item.BottomRight, item.BorderColor, 0, ImDrawFlags.None, 2);
            if (item.Text != null)
            {
                var textPos = new System.Numerics.Vector2(item.TopLeft.X + 2, item.TopLeft.Y + 1);
                // Draw text shadow for readability
                drawList.AddText(new System.Numerics.Vector2(textPos.X + 1, textPos.Y + 1), 0xFF000000, item.Text);
                drawList.AddText(textPos, item.TextColor, item.Text);
            }
        }
    }

    private List<OverlayDrawItem> BuildItems()
    {
        var drawItems = new List<OverlayDrawItem>();
        var settings = TujenMem.Instance.Settings.MemoryMapPriceSettings;
        var ui = TujenMem.Instance.GameController.IngameState.IngameUi;

        // Scan player inventory
        if (settings.ShowOnInventory)
        {
            var inventory = ui.InventoryPanel[InventoryIndex.PlayerInventory];
            if (inventory is { IsVisible: true })
                ScanItems(inventory.VisibleInventoryItems, drawItems, settings);
        }

        // Scan visible stash tab
        if (settings.ShowOnStash)
        {
            var stash = ui.StashElement;
            if (stash is { IsVisible: true })
            {
                var visibleStash = stash.VisibleStash;
                if (visibleStash?.VisibleInventoryItems != null)
                    ScanItems(visibleStash.VisibleInventoryItems, drawItems, settings);
            }
        }

        return drawItems;
    }

    private static void ScanItems(IEnumerable<NormalInventoryItem> items,
                                   List<OverlayDrawItem> drawItems,
                                   MemoryMapPriceSettings settings)
    {
        if (items == null) return;

        foreach (var invItem in items)
        {
            try
            {
                var entity = invItem.Item;
                if (entity == null) continue;

                if (!MemoryMapPriceModel.IsMemoryInfluencedMap(entity))
                    continue;

                var stats = MemoryMapPriceModel.ExtractStats(entity);
                if (stats == null || stats.Quantity == 0) continue;

                double rawPrice = MemoryMapPriceModel.EstimatePrice(
                    stats.Quantity, stats.Rarity, stats.PackSize,
                    stats.MoreMaps, stats.MoreCurrency,
                    settings.ScarcityWeight / 100.0,
                    settings.CurrencyThreshold,
                    settings.RarityThreshold,
                    settings.QuantityThreshold,
                    settings.CurrencyCeiling,
                    settings.RarityCeiling,
                    settings.QuantityCeiling);

                double displayPrice = MemoryMapPriceModel.AdjustedPrice(
                    rawPrice, settings.DiscountPct, settings.Percentile);

                string label = MemoryMapPriceModel.FormatPrice(displayPrice, settings.DivineRate);

                // Determine colors based on value tier
                uint textColor, fillColor, borderColor;
                if (displayPrice >= settings.GreatThreshold)
                {
                    textColor = ColorGreat;
                    fillColor = ColorFillGreat;
                    borderColor = ColorBorderGreat;
                }
                else if (displayPrice >= settings.GoodThreshold)
                {
                    textColor = ColorGood;
                    fillColor = ColorFillGood;
                    borderColor = ColorBorderGood;
                }
                else
                {
                    textColor = ColorGrey;
                    fillColor = ColorFillLow;
                    borderColor = 0; // no border for cheap maps
                }

                var rect = invItem.GetClientRect();
                var topLeft = new System.Numerics.Vector2(rect.X, rect.Y);
                var bottomRight = new System.Numerics.Vector2(rect.X + rect.Width, rect.Y + rect.Height);

                // Full cell color overlay
                drawItems.Add(new OverlayDrawItem
                {
                    TopLeft = topLeft,
                    BottomRight = bottomRight,
                    FillColor = fillColor,
                    BorderColor = borderColor,
                });

                // Price label in top-left corner
                var textSize = ImGui.CalcTextSize(label);
                var labelTL = new System.Numerics.Vector2(rect.X + 2, rect.Y + 2);
                var labelBR = new System.Numerics.Vector2(labelTL.X + textSize.X + 4, labelTL.Y + textSize.Y + 2);

                drawItems.Add(new OverlayDrawItem
                {
                    TopLeft = labelTL,
                    BottomRight = labelBR,
                    FillColor = ColorBgDark,
                    Text = label,
                    TextColor = textColor,
                });
            }
            catch (Exception)
            {
                // Skip items that fail to read (stale memory, etc.)
            }
        }
    }
}
