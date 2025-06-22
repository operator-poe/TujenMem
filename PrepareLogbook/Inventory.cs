using System.Linq;
using System.Collections.Generic;
using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.Components;
using SharpDX;
using ExileCore;

namespace TujenMem.PrepareLogbook;

public class Inventory
{
  public List<ReRollable> Logbooks { get; set; } = new List<ReRollable>();

  private static List<Logbook> _cachedLogbooks = new List<Logbook>();
  private static int _lastInventoryItemCount = 0;

  public Inventory()
  {
    Logbooks = new List<ReRollable>();
    var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
    var inventoryItems = inventory.VisibleInventoryItems;
    for (int i = 0; i < inventoryItems.Count(); i++)
    {
      if (inventoryItems.ElementAt(i).Item.Path.Contains("ExpeditionLogbook"))
      {
        Logbooks.Add(new Logbook
        {
          GridPosition = inventoryItems.ElementAt(i).GetClientRect(),
        });
      }
      else if
       (inventoryItems.ElementAt(i).Item.Path.Contains("/Maps/"))
      {
        Logbooks.Add(new Map
        {
          GridPosition = inventoryItems.ElementAt(i).GetClientRect(),
          Quality = inventoryItems.ElementAt(i).Item.GetComponent<Quality>()?.ItemQuality ?? 0,
          Tier = inventoryItems.ElementAt(i).Item.GetComponent<ExileCore.PoEMemory.Components.Map>()?.Tier ?? 0,
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

  public static void RenderOverlay()
  {
    var settings = TujenMem.Instance.Settings;
    if (!settings.SillyOrExperimenalFeatures.ShowLogbookOverlay ||
        TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory] is not { IsVisible: true })
      return;

    var inventoryPanel = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
    var inventoryItems = inventoryPanel.VisibleInventoryItems;

    // Only rebuild cache if inventory items changed
    if (inventoryItems.Count != _lastInventoryItemCount)
    {
      _cachedLogbooks.Clear();
      foreach (var item in inventoryItems)
      {
        if (item.Item.Path.Contains("ExpeditionLogbook"))
        {
          _cachedLogbooks.Add(new Logbook
          {
            GridPosition = item.GetClientRect()
          });
        }
      }
      _lastInventoryItemCount = inventoryItems.Count;
    }

    // Render cached logbooks
    foreach (var logbook in _cachedLogbooks)
    {
      if (logbook.Item != null)
      {
        var position = logbook.Position;
        var quantity = logbook.Quantity ?? 0;
        var prepareSettings = settings.PrepareLogbookSettings;

        // Determine if logbook is ready based on quantity and other criteria
        bool isReady = TujenMem.Instance.IsLogbookReady(logbook, prepareSettings);

        // Choose color based on readiness
        Color textColor = isReady ? Color.Green : Color.Red;
        Color borderColor = isReady ? Color.Green : Color.Red;

        // Draw border around the logbook
#pragma warning disable CS0612 // Type or member is obsolete
        TujenMem.Instance.Graphics.DrawFrame(position.TopLeft, position.BottomRight, borderColor, 2);
#pragma warning restore CS0612 // Type or member is obsolete

        // Draw quantity percentage text
        string quantityText = $"{quantity}%";
        var textSize = TujenMem.Instance.Graphics.MeasureText(quantityText, 12);
        var textX = position.X + (position.Width - textSize.X) / 2;
        var textY = position.Y + 2;

        // Draw semi-transparent background for text
        var textBgRect = new RectangleF(textX - 2, textY - 1, textSize.X + 4, textSize.Y + 2);
#pragma warning disable CS0612 // Type or member is obsolete
        TujenMem.Instance.Graphics.DrawBox(textBgRect, new Color(0, 0, 0, 180)); // Semi-transparent black background
        TujenMem.Instance.Graphics.DrawText(quantityText, new Vector2(textX, textY), textColor, 12);
#pragma warning restore CS0612 // Type or member is obsolete

        // Draw readiness indicator at bottom
        string statusText = isReady ? "READY" : "NEEDS ROLL";
        var statusTextSize = TujenMem.Instance.Graphics.MeasureText(statusText, 10);
        var statusX = position.X + (position.Width - statusTextSize.X) / 2;
        var statusY = position.Y + position.Height - statusTextSize.Y - 2;

        // Draw semi-transparent background for status text
        var statusBgRect = new RectangleF(statusX - 2, statusY - 1, statusTextSize.X + 4, statusTextSize.Y + 2);
#pragma warning disable CS0612 // Type or member is obsolete
        TujenMem.Instance.Graphics.DrawBox(statusBgRect, new Color(0, 0, 0, 180)); // Semi-transparent black background
        TujenMem.Instance.Graphics.DrawText(statusText, new Vector2(statusX, statusY), textColor, 10);
#pragma warning restore CS0612 // Type or member is obsolete
      }
    }
  }
}