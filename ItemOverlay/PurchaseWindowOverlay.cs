using System;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class PurchaseWindowOverlay : IInventoryOverlayRenderer
{
    public bool ShouldRender()
    {
        return TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay &&
               TujenMem.Instance.GameController.IngameState.IngameUi
                   .PurchaseWindow is { IsVisible: true };
    }

    public void Render(ImDrawListPtr drawList)
    {
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
            var pos = new System.Numerics.Vector2(rect.X + 2, rect.Y + 2);
            var textSize = ImGui.CalcTextSize(label);

            drawList.AddRectFilled(
                new System.Numerics.Vector2(pos.X - 2, pos.Y - 1),
                new System.Numerics.Vector2(pos.X + textSize.X + 2, pos.Y + textSize.Y + 1),
                0xB4000000);
            drawList.AddText(pos, 0xFFFFFFFF, label);
        }
    }
}
