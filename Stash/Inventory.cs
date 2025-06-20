using System.Collections.Generic;
using System.Data;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using TujenMem;

namespace TujenMem.Stash;

public static class Inventory
{
  private static ServerInventory ServerInventory
  {
    get
    {
      return TujenMem.Instance.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
    }
  }

  public static IList<InventSlotItem> InventoryItems
  {
    get
    {
      return ServerInventory.InventorySlotItems;
    }
  }

  public static InventSlotItem[] GetByName(params string[] names)
  {
    var items = new List<InventSlotItem>();
    var nameSet = new HashSet<string>(names);

    foreach (var item in InventoryItems)
    {
      var baseComponent = item.Item?.GetComponent<Base>();
      Log.Debug(baseComponent.Name);
      if (baseComponent != null && nameSet.Contains(baseComponent.Name))
      {
        items.Add(item);
      }
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }

  public static InventSlotItem[] GetByType(string type)
  {
    var items = new List<InventSlotItem>();

    foreach (var item in InventoryItems)
    {
      if (item.Item.Path.Contains(type))
      {
        items.Add(item);
      }
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }


  public static InventSlotItem[] GetByModName(List<(string, int)> names)
  {
    var items = new List<InventSlotItem>();

    foreach (var (modName, quantity) in names)
    {
      var i = GetByModName(modName);
      items.AddRange(i.Take(quantity));
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }

  public static InventSlotItem[] GetByModName(List<string> names)
  {
    var items = new List<InventSlotItem>();

    foreach (var item in InventoryItems)
    {
      if (item.Item.HasComponent<Mods>())
      {
        var mods = item.Item.GetComponent<Mods>();
        foreach (var m in mods.ItemMods)
        {
          if (names.Contains(m.RawName))
          {
            items.Add(item);
            break;
          }
        }
      }
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToArray();
  }

  public static InventSlotItem[] GetByModName(string name)
  {
    return GetByModName(new List<string> { name });
  }

  public static int InventoryCount
  {
    get
    {
      return InventoryItems.Count(x => x.Item != null);
    }
  }
  public static int FreeInventorySlots
  {
    get
    {
      return 60 - InventoryCount;
    }
  }

  public static InventSlotItem GetSlotAt(int x, int y)
  {
    foreach (var item in InventoryItems)
    {
      if (item.PosX == x && item.PosY == y)
      {
        return item;
      }
    }
    return null;
  }
  public static Item NextFreeInventoryPositinon
  {
    get
    {
      var inventoryPanel = TujenMem.Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      RectangleF inventoryPanelPosition = inventoryPanel.InventoryUIElement.GetClientRect();

      float cellSize = 0;
      for (int x = 0; x < 12; x++)
      {
        for (int y = 0; y < 5; y++)
        {
          var slot = GetSlotAt(x, y);
          if (slot != null && slot.Item != null)
          {
            cellSize = slot.GetClientRect().Height;
            break;
          }
        }
        if (cellSize != 0)
        {
          break;
        }
      }



      for (int x = 0; x < 12; x++)
      {
        for (int y = 0; y < 5; y++)
        {
          var slot = GetSlotAt(x, y);
          if (slot == null)
          {
            inventoryPanelPosition.X += cellSize * x + cellSize / 2f;
            inventoryPanelPosition.Y += cellSize * y + cellSize / 2f;
            return new Item(inventoryPanelPosition);
          }
        }
      }

      return null;
    }
  }
}
