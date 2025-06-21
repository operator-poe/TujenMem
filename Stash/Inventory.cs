using System;
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

public class InventorySnapshot
{
  public bool[,] OccupiedSlots { get; set; } = new bool[12, 5]; // 12x5 grid
  public Dictionary<(int x, int y), InventSlotItem> Items { get; set; } = new Dictionary<(int x, int y), InventSlotItem>();
  public DateTime CreatedAt { get; set; } = DateTime.Now;

  public bool IsOccupied(int x, int y)
  {
    if (x < 0 || x >= 12 || y < 0 || y >= 5)
      return false;
    return OccupiedSlots[x, y];
  }

  public InventSlotItem GetItemAt(int x, int y)
  {
    return Items.TryGetValue((x, y), out var item) ? item : null;
  }

  public List<InventSlotItem> GetAllItems()
  {
    return Items.Values.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();
  }

  public List<InventSlotItem> GetItemsInRange(int startX, int endX, int startY, int endY)
  {
    var items = new List<InventSlotItem>();
    for (int x = startX; x <= endX; x++)
    {
      for (int y = startY; y <= endY; y++)
      {
        if (IsOccupied(x, y))
        {
          var item = GetItemAt(x, y);
          if (item != null)
            items.Add(item);
        }
      }
    }
    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();
  }
}

public static class Inventory
{
  private static InventorySnapshot _baselineSnapshot = null;

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

  /// <summary>
  /// Sets the baseline snapshot for future comparisons
  /// </summary>
  /// <param name="snapshot">The baseline snapshot to use</param>
  public static void SetBaselineSnapshot(InventorySnapshot snapshot)
  {
    _baselineSnapshot = snapshot;
  }

  /// <summary>
  /// Gets the current baseline snapshot
  /// </summary>
  /// <returns>The baseline snapshot or null if not set</returns>
  public static InventorySnapshot GetBaselineSnapshot()
  {
    return _baselineSnapshot;
  }

  /// <summary>
  /// Creates a baseline snapshot from the current inventory state
  /// </summary>
  public static void CreateBaselineSnapshot()
  {
    _baselineSnapshot = CreateSnapshot();
  }

  /// <summary>
  /// Calculates the difference between the baseline snapshot and the current inventory
  /// </summary>
  /// <returns>List of items that were added since the baseline</returns>
  public static List<InventSlotItem> CalculateDiffFromBaseline()
  {
    if (_baselineSnapshot == null)
    {
      Log.Debug("No baseline snapshot available, returning all items");
      return InventoryItems.Where(x => x?.Item != null).OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();
    }

    var currentSnapshot = CreateSnapshot();
    return CalculateDiff(_baselineSnapshot, currentSnapshot);
  }

  /// <summary>
  /// Manually refreshes the baseline snapshot (useful for debugging or resetting tracking)
  /// </summary>
  public static void RefreshBaselineSnapshot()
  {
    Log.Debug("Refreshing baseline snapshot");
    CreateBaselineSnapshot();
  }

  /// <summary>
  /// Gets debug information about the current snapshot state
  /// </summary>
  /// <returns>Debug string with snapshot information</returns>
  public static string GetSnapshotDebugInfo()
  {
    var currentSnapshot = CreateSnapshot();
    var baselineCount = _baselineSnapshot?.GetAllItems().Count ?? 0;
    var currentCount = currentSnapshot.GetAllItems().Count;
    var diffCount = CalculateDiffFromBaseline().Count;

    return $"Baseline: {baselineCount} items, Current: {currentCount} items, New since baseline: {diffCount} items";
  }

  /// <summary>
  /// Creates a snapshot of the current inventory state
  /// </summary>
  /// <returns>InventorySnapshot representing the current inventory state</returns>
  public static InventorySnapshot CreateSnapshot()
  {
    var snapshot = new InventorySnapshot();

    foreach (var item in InventoryItems)
    {
      if (item?.Item != null && item.PosX >= 0 && item.PosX < 12 && item.PosY >= 0 && item.PosY < 5)
      {
        snapshot.OccupiedSlots[item.PosX, item.PosY] = true;
        snapshot.Items[(item.PosX, item.PosY)] = item;
      }
    }

    return snapshot;
  }

  /// <summary>
  /// Calculates the difference between two inventory snapshots
  /// </summary>
  /// <param name="before">The snapshot before changes</param>
  /// <param name="after">The snapshot after changes</param>
  /// <returns>List of items that were added (present in after but not in before)</returns>
  public static List<InventSlotItem> CalculateDiff(InventorySnapshot before, InventorySnapshot after)
  {
    var newItems = new List<InventSlotItem>();

    for (int x = 0; x < 12; x++)
    {
      for (int y = 0; y < 5; y++)
      {
        // If slot is occupied in after but not in before, it's a new item
        if (after.IsOccupied(x, y) && !before.IsOccupied(x, y))
        {
          var item = after.GetItemAt(x, y);
          if (item != null)
            newItems.Add(item);
        }
      }
    }

    return newItems.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();
  }

  /// <summary>
  /// Gets all items in the inventory that are not in the excluded rows (default: rows 10-11)
  /// </summary>
  /// <param name="excludeStartRow">Starting row to exclude (inclusive)</param>
  /// <param name="excludeEndRow">Ending row to exclude (inclusive)</param>
  /// <returns>List of items to stash</returns>
  public static List<InventSlotItem> GetItemsToStash(int excludeStartRow = 10, int excludeEndRow = 11)
  {
    var items = new List<InventSlotItem>();

    foreach (var item in InventoryItems)
    {
      if (item?.Item != null && (item.PosX < excludeStartRow || item.PosX > excludeEndRow))
      {
        items.Add(item);
      }
    }

    return items.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();
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
