using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ImGuiNET;
using TujenMem.ItemRoller;

namespace TujenMem.ItemOverlay;

public class NormalInventoryOverlay : IInventoryOverlayRenderer
{
    private readonly ItemRollerManager _rollerManager;
    private readonly CachedValue<List<OverlayDrawItem>> _cache;

    public NormalInventoryOverlay(ItemRollerManager rollerManager)
    {
        _rollerManager = rollerManager;
        _cache = new TimeCache<List<OverlayDrawItem>>(BuildCache, 250);
    }

    public bool ShouldRender()
    {
        var hasEnabledOverlay = _rollerManager.Rollers.Any(r => r is PrepareLogbook.LogbookRoller &&
            TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay) ||
            _rollerManager.Rollers.Any(r => r is MapRoller.MapRoller &&
            TujenMem.Instance.Settings.MapRollerSettings.ShowItemOverlay);

        return hasEnabledOverlay &&
               TujenMem.Instance.GameController.IngameState.IngameUi
                   .InventoryPanel[InventoryIndex.PlayerInventory] is { IsVisible: true };
    }

    private List<OverlayDrawItem> BuildCache()
    {
        var items = new List<OverlayDrawItem>();
        var inventoryPanel = TujenMem.Instance.GameController.IngameState.IngameUi
            .InventoryPanel[InventoryIndex.PlayerInventory];
        if (inventoryPanel == null) return items;

        foreach (var invItem in inventoryPanel.VisibleInventoryItems)
        {
            try
            {
                var entity = invItem.Item;
                var path = entity?.Path;
                if (path == null) continue;

                var roller = _rollerManager.FindRoller(path);
                if (roller == null) continue;

                var showOverlay = roller switch
                {
                    PrepareLogbook.LogbookRoller => TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay.Value,
                    MapRoller.MapRoller => TujenMem.Instance.Settings.MapRollerSettings.ShowItemOverlay.Value,
                    _ => false,
                };
                if (!showOverlay) continue;

                var rerollable = roller.CreateReRollable(invItem);
                var readiness = roller.GetReadiness(rerollable);

                uint fillColor, borderColor;
                switch (readiness)
                {
                    case ItemReadiness.Ready:
                        fillColor = 0x4000FF00;
                        borderColor = 0xFF00FF00;
                        break;
                    case ItemReadiness.ModsOkQuantityLow:
                        fillColor = 0x4000FFFF;
                        borderColor = 0xFF00FFFF;
                        break;
                    default:
                        fillColor = 0x400000FF;
                        borderColor = 0xFF0000FF;
                        break;
                }

                var rect = invItem.GetClientRect();
                items.Add(new OverlayDrawItem
                {
                    TopLeft = new System.Numerics.Vector2(rect.X, rect.Y),
                    BottomRight = new System.Numerics.Vector2(rect.X + rect.Width, rect.Y + rect.Height),
                    FillColor = fillColor,
                    BorderColor = borderColor,
                });
            }
            catch (Exception)
            {
                // Skip items that fail to read (stale memory, etc.)
            }
        }

        return items;
    }

    public void Render(ImDrawListPtr drawList)
    {
        foreach (var item in _cache.Value)
        {
            if (item.FillColor != 0)
                drawList.AddRectFilled(item.TopLeft, item.BottomRight, item.FillColor);
            if (item.BorderColor != 0)
                drawList.AddRect(item.TopLeft, item.BottomRight, item.BorderColor, 0, ImDrawFlags.None, 2);
        }
    }
}
