using System.Collections.Generic;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using System.Windows.Forms;

namespace TujenMem;

public static class GwennenRunner
{
  private static TujenMem Instance = TujenMem.Instance;
  private static bool FreshRun = true;

  public static NormalInventoryItem HasNonUniqueItemsInInventory
  {
    get
    {
      var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      var inventoryItems = inventory.VisibleInventoryItems;
      foreach (var item in inventoryItems)
      {
        var itemBase = item.Item.GetComponent<ExileCore.PoEMemory.Components.Base>();
        if (itemBase.Name.Contains("Amulet"))
        {
          var mods = item.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
          if (mods != null && mods.ItemMods != null && mods.ItemMods.Count > 0)
          {
            foreach (var mod in mods.ItemMods)
            {
              if (mod.Group.Contains("GlobalSkillGemLevel"))
              {
                continue;
              }

            }
          }
        }
        var rarity = item.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>()?.ItemRarity;
        if (rarity == ItemRarity.Rare || rarity == ItemRarity.Magic)
        {
          return item;
        }
      }
      return null;
    }
  }

  public static bool HasEnoughStock
  {
    get
    {
      return HaggleStock.Coins > 0 && HaggleStock.Lesser > 100 && HaggleStock.Greater > 100 && HaggleStock.Grand > 100 && HaggleStock.Exceptional > 100;
    }
  }


  public static bool CanGwennen()
  {
    return Instance.GameController.IngameState.IngameUi.HaggleWindow.IsVisible
     && Instance.GameController.IngameState.IngameUi.HaggleWindow.VendorWindowTitle == "Gamble";

  }
  public static async SyncTask<bool> Run()
  {
    HaggleStock.StockType = StockType.Gwennen;
    FreshRun = true;
    HashSet<string> baseTypes = new HashSet<string>(Instance.Settings.Gwennen.BaseList);
    while (HasEnoughStock)
    {
      await InputAsync.WaitX(3);
      await ProcessWindow(baseTypes);
      await InputAsync.Wait();

      var oldCount = HaggleStock.Coins;
      await Instance.ReRollWindow();
      await InputAsync.Wait(() => oldCount > HaggleStock.Coins, 500);
      await InputAsync.WaitX(5);
      if (oldCount == HaggleStock.Coins)
      {
        Error.AddAndShow("Error", "Window did not reroll after attempting a click.\nCheck your hover delay and make sure that the window is not obstructed.");
        Instance.StopAllRoutines();
        return false;
      }
    }
    await DeleteNonUniquesInInventory();
    Instance.StopAllRoutines();
    return true;
  }

  public static async SyncTask<bool> ProcessWindow(HashSet<string> baseTypes)
  {
    foreach (var item in Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems)
    {
      if (!HasEnoughStock)
      {
        return false;
      }
      var itemBase = item.Item.GetComponent<ExileCore.PoEMemory.Components.Base>();
      var itemBaseName = itemBase?.Name;
      if (baseTypes.Contains(itemBaseName))
      {
        var itemCount = Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count;
        await InputAsync.KeyDown(Keys.ControlKey);
        await InputAsync.ClickElement(item.GetClientRect());
        await InputAsync.KeyUp(Keys.ControlKey);
        await InputAsync.Wait(() => Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count < itemCount, 500);
        if (Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count == itemCount)
        {
          await DeleteNonUniquesInInventory();
          await InputAsync.KeyDown(Keys.ControlKey);
          await InputAsync.ClickElement(item.GetClientRect());
          await InputAsync.KeyUp(Keys.ControlKey);
          await InputAsync.Wait(() => Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count < itemCount, 500);
          if (Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count == itemCount)
          {
            Error.AddAndShow("Error", "Item could not be bought, probably not enough inventory space");
            return false;
          }
        }
        await InputAsync.Wait();
      }
    }
    return true;
  }

  public static async SyncTask<bool> DeleteNonUniquesInInventory()
  {
    Log.Debug("Deleting non uniques in inventory");
    while (HasNonUniqueItemsInInventory != null)
    {
      var item = HasNonUniqueItemsInInventory;
      var rarity = item.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>()?.ItemRarity;
      if (rarity == ItemRarity.Rare || rarity == ItemRarity.Magic)
      {
        Log.Debug($"Deleting {item.Item.Path}");
        await InputAsync.ClickElement(item.GetClientRect());
        if (FreshRun)
        {
          await Chat.Send(["/destroy"]);
          FreshRun = false;
        }
        else
        {
          await Chat.Repeat("/destroy");
        }
        Log.Debug("Deleted");
      }
    }

    return true;
  }
}