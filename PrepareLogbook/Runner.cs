using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using TujenMem.ItemRoller;

namespace TujenMem.PrepareLogbook;

public class Runner
{
  public static string CoroutineNameRollAndBless { get; set; } = "TujenMem_PrepareLogbook_RollAndBless";

  private static ItemRollerManager _rollerManager;

  public static void SetRollerManager(ItemRollerManager manager)
  {
    _rollerManager = manager;
  }

  public static async SyncTask<bool> RollAndBlessLogbooksCoroutine()
  {
    Log.Debug("Starting RollAndBlessLogbooksCoroutine");

    if (_rollerManager == null)
    {
      Log.Error("RollerManager not set");
      return false;
    }

    return await _rollerManager.RollAll();
  }

  public static async SyncTask<bool> IdentifyItemsInStash()
  {
    Log.Debug("Instantiating Stash");

    var currencyNames = new[] { "Scroll of Wisdom" };
    var stash = new RollerStash(currencyNames, false);

    var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];

    Log.Debug("Getting Wisdom");
    NormalInventoryItem wisdom = null;
    foreach (var item in inventory.VisibleInventoryItems)
    {
      var itemPath = item.Item.Path;
      if (itemPath.Contains("CurrencyIdentification"))
      {
        wisdom = item;
        break;
      }
    }
    if (wisdom == null) return false;

    var scroll = new CurrencyByPos
    {
      Position = wisdom.GetClientRect(),
    };

    Log.Debug("Getting Unidentified Uniques");
    var unidentifiedUniques = stash.GetUnidentifiedUniques();
    if (unidentifiedUniques.Count == 0) return false;
    await scroll.Hold();
    foreach (var unique in unidentifiedUniques)
    {
      await InputAsync.ClickElement(unique.GetClientRect(), System.Windows.Forms.MouseButtons.Left);
      await InputAsync.WaitX(5);
    }
    await scroll.Release();

    return await stash.CleanUp();
  }
}
