
using ExileCore.Shared.Enums;
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
      var price = baseItem.PublicPrice; // example: ~b/o 2 chaos
    }
  }
}