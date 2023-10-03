using System.Linq;
using System.Collections.Generic;
using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.Components;
using SharpDX;

namespace TujenMem.PrepareLogbook;

public class Inventory
{
  public List<Logbook> Logbooks { get; set; } = new List<Logbook>();

  public Inventory()
  {
    Logbooks = new List<Logbook>();
    var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
    var inventoryItems = inventory.VisibleInventoryItems;
    for (int i = 0; i < inventoryItems.Count(); i++)
    {
      if (inventoryItems.ElementAt(i).Item.Path.Contains("ExpeditionLogbook"))
      {
        Logbooks.Add(new Logbook
        {
          GridPosition = inventoryItems.ElementAt(i).GetClientRect().Center
        });
      }
    }
  }

  public static int DivineCount
  {
    get
    {
      var items = TujenMem.Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
      var totalDivines = 0;
      foreach (var item in items)
      {
        var baseItem = item.Item.GetComponent<Base>();
        if (baseItem?.Name == "Divine Orb")
        {
          var stack = item.Item.GetComponent<Stack>();
          totalDivines += stack.Size;
        }
      }
      return totalDivines;
    }
  }

  public static int ChaosCount
  {
    get
    {
      var items = TujenMem.Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
      var totalChaos = 0;
      foreach (var item in items)
      {
        var baseItem = item.Item.GetComponent<Base>();
        if (baseItem?.Name == "Chaos Orb")
        {
          var stack = item.Item.GetComponent<Stack>();
          totalChaos += stack.Size;
        }
      }
      return totalChaos;
    }
  }

  public static Vector2 NextFreePosition
  {
    get
    {
      var inventory = TujenMem.Instance.GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
      var inventorySlotItems = inventory.InventorySlotItems;
      int gridWidth = inventory.Columns;
      int gridHeight = inventory.Rows;

      int cellWidth = 50;
      int cellHeight = 50;

      var inventoryPanel = TujenMem.Instance.GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      Vector2 position = inventoryPanel.InventoryUIElement.GetClientRect().TopLeft;

      if (inventorySlotItems.Count > 0)
      {
        var item = inventorySlotItems.First();

        cellWidth = (int)item.GetClientRect().Width;
        cellHeight = (int)item.GetClientRect().Height;
      }
      else
      {
        position.X += cellWidth;
        position.Y += cellHeight;

        return position;
      }

      bool[,] grid = new bool[gridWidth, gridHeight]; // False by default, representing free slots

      foreach (var item in inventorySlotItems)
      {
        grid[item.PosX, item.PosY] = true; // Mark the slot as occupied
      }

      for (int x = 0; x < gridWidth; x++)
      {
        for (int y = 0; y < gridHeight; y++)
        {
          if (!grid[x, y])
          {
            position.X += cellWidth * x;
            position.X += cellWidth / 2;
            position.Y += cellHeight * y;
            position.Y += cellHeight / 2;
            return position;
          }
        }
      }
      return Vector2.Zero;
    }
  }
}