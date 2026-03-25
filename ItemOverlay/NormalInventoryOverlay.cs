using System.Collections.Generic;
using ExileCore.Shared.Enums;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class NormalInventoryOverlay : IInventoryOverlayRenderer
{
    private List<OverlayDrawItem> _cache = new();

    public bool ShouldRender()
    {
        return TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay &&
               TujenMem.Instance.GameController.IngameState.IngameUi
                   .InventoryPanel[InventoryIndex.PlayerInventory] is { IsVisible: true };
    }

    public void Update()
    {
        var items = new List<OverlayDrawItem>();
        var inventoryPanel = TujenMem.Instance.GameController.IngameState.IngameUi
            .InventoryPanel[InventoryIndex.PlayerInventory];
        var prepSettings = TujenMem.Instance.Settings.PrepareLogbookSettings;

        foreach (var invItem in inventoryPanel.VisibleInventoryItems)
        {
            var path = invItem.Item?.Path;
            if (path == null) continue;

            PrepareLogbook.ReRollable rerollable = null;
            if (path.Contains("ExpeditionLogbook"))
            {
                rerollable = new PrepareLogbook.Logbook { GridPosition = invItem.GetClientRect() };
            }
            else if (path.Contains("/Maps/"))
            {
                rerollable = new PrepareLogbook.Map { GridPosition = invItem.GetClientRect() };
            }

            if (rerollable == null) continue;

            var readiness = TujenMem.Instance.GetItemReadiness(rerollable, prepSettings);
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

        _cache = items;
    }

    public void Render(ImDrawListPtr drawList)
    {
        foreach (var item in _cache)
        {
            if (item.FillColor != 0)
                drawList.AddRectFilled(item.TopLeft, item.BottomRight, item.FillColor);
            if (item.BorderColor != 0)
                drawList.AddRect(item.TopLeft, item.BottomRight, item.BorderColor, 0, ImDrawFlags.None, 2);
        }
    }
}
