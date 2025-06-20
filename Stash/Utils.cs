using TujenMem;
using ExileCore.Shared;
using ExileCore;
using ExileCore.PoEMemory.Elements;

namespace TujenMem.Stash;

public static class Utils
{
  public static GameController GameController
  {
    get
    {
      return TujenMem.Instance.GameController;
    }
  }
  public static async SyncTask<bool> EnsureEverythingIsClosed()
  {
    if (GameController.IngameState.IngameUi.InventoryPanel != null && GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
    {
      await InputAsync.KeyDown(TujenMem.Instance.Settings.HotKeySettings.InventoryHotKey.Value);
      await InputAsync.KeyUp(TujenMem.Instance.Settings.HotKeySettings.InventoryHotKey.Value);
      await InputAsync.Wait(() => !GameController.IngameState.IngameUi.InventoryPanel.IsVisible, 1000, "Inventory not closed");
    }
    if (GameController.IngameState.IngameUi.StashElement != null && GameController.IngameState.IngameUi.StashElement.IsVisible)
    {
      await InputAsync.KeyDown(System.Windows.Forms.Keys.Escape);
      await InputAsync.KeyUp(System.Windows.Forms.Keys.Escape);
      await InputAsync.Wait(() => !GameController.IngameState.IngameUi.StashElement.IsVisible, 1000, "Inventory not closed");
    }
    return true;
  }
  public static async SyncTask<bool> EnsureStash()
  {
    if (GameController.IngameState.IngameUi.StashElement.IsVisible && GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
    {
      return true;
    }
    await EnsureEverythingIsClosed();

    var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
    var stash = GameController.IngameState.IngameUi.StashElement;

    if (stash is { IsVisible: true })
    {
      return true;
    }

    foreach (LabelOnGround labelOnGround in itemsOnGround)
    {
      if (!(labelOnGround?.ItemOnGround?.Path?.Contains("/Stash") ?? true))
      {
        continue;
      }
      if (!labelOnGround.IsVisible)
      {
        Log.Error("Stash not visible");
        return false;
      }
      await InputAsync.ClickElement(labelOnGround.Label.GetClientRect());
      await InputAsync.Wait(() => stash is { IsVisible: true }, 2000, "Stash not reached in time");
      if (stash is { IsVisible: false })
      {
        Log.Error("Stash not visible");
        return false;
      }
    }
    return true;
  }
}