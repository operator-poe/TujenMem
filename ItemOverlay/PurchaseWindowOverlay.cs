using System;
using System.Collections.Generic;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class PurchaseWindowOverlay : IInventoryOverlayRenderer
{
    private List<OverlayDrawItem> _cache = new();

    public bool ShouldRender()
    {
        return TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay &&
               TujenMem.Instance.GameController.IngameState.IngameUi
                   .PurchaseWindow is { IsVisible: true };
    }

    public void Update()
    {
        var items = new List<OverlayDrawItem>();
        var purchaseWindow = TujenMem.Instance.GameController.IngameState.IngameUi.PurchaseWindow;
        var visibleItems = purchaseWindow.TabContainer.VisibleStash.VisibleInventoryItems;

        foreach (var item in visibleItems)
        {
            var entity = item.Entity;
            var baseItem = entity.GetComponent<ExileCore.PoEMemory.Components.Base>();
            var price = baseItem?.PublicPrice;

            if (string.IsNullOrEmpty(price)) continue;

            var parts = price.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var amount = parts[^2];
            var currency = parts[^1].ToLowerInvariant() switch
            {
                "chaos" => "c",
                "divine" => "d",
                _ => "X",
            };
            var label = $"{amount}{currency}";

            var rect = item.GetClientRect();
            var textSize = ImGui.CalcTextSize(label);
            var pos = new System.Numerics.Vector2(rect.X + 2, rect.Y + 2);

            items.Add(new OverlayDrawItem
            {
                TopLeft = new System.Numerics.Vector2(pos.X - 2, pos.Y - 1),
                BottomRight = new System.Numerics.Vector2(pos.X + textSize.X + 2, pos.Y + textSize.Y + 1),
                FillColor = 0xB4000000,
                Text = label,
                TextColor = 0xFFFFFFFF,
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
            if (item.Text != null)
            {
                var textPos = new System.Numerics.Vector2(item.TopLeft.X + 2, item.TopLeft.Y + 1);
                drawList.AddText(textPos, item.TextColor, item.Text);
            }
        }
    }
}
