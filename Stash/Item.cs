using ExileCore.PoEMemory.Elements.InventoryElements;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using SharpDX;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using ExileCore.Shared;
using TujenMem;

namespace TujenMem.Stash;

public enum ItemType
{
  Sextant,
  Compass,
  ChargedCompass
}

public class Item
{
  public static Dictionary<ItemType, string> ItemNames = new Dictionary<ItemType, string>()
  {
    { ItemType.Sextant, "Awakened Sextant" },
    { ItemType.Compass, "Surveyor's Compass" },
    { ItemType.ChargedCompass, "Charged Compass" }
  };

  private static TujenMem Instance = TujenMem.Instance;
  public RectangleF Position { get; set; }

  public Item(NormalInventoryItem item)
  {
    Position = item.GetClientRect();
  }

  public Item(InventSlotItem item)
  {
    Position = item.GetClientRect();
  }

  public Item(RectangleF position)
  {
    Position = position;
  }

  public async SyncTask<bool> Hover()
  {
    await InputAsync.MoveMouseToElement(Position);
    await InputAsync.Wait();
    return true;
  }

  public async SyncTask<bool> GetStack(bool CtrlClick = false)
  {
    if (CtrlClick)
      await InputAsync.KeyDown(Keys.ControlKey);
    await InputAsync.ClickElement(Position);
    if (CtrlClick)
      await InputAsync.KeyUp(Keys.ControlKey);
    return true;
  }

  public async SyncTask<bool> GetFraction(int num)
  {
    await Hover();
    await InputAsync.KeyDown(Keys.ShiftKey);
    await InputAsync.Click(MouseButtons.Left);
    await InputAsync.KeyUp(Keys.ShiftKey);
    await InputAsync.Wait(() => Instance.GameController.IngameState.IngameUi.CurrencyShiftClickMenu is { IsVisible: true }, 1000, "Split window not opened");
    var numAsString = num.ToString();

    // iterate each number and send the key
    foreach (var c in numAsString)
    {
      var key = (Keys)c;
      await InputAsync.KeyDown(key);
      await InputAsync.KeyUp(key);
      await InputAsync.Wait();
    }
    await InputAsync.Wait();
    await InputAsync.KeyDown(Keys.Enter);
    await InputAsync.KeyUp(Keys.Enter);
    await InputAsync.Wait();
    return true;
  }
}
