using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ItemFilterLibrary;
using SharpDX;

namespace TujenMem.ItemRoller;

public class ReRollable
{
  private static readonly Regex QuantityRegex = new(@"\+(\d+)%.*quantity", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex QuantityTooltipRegex = new(@"\+(\d+)%", RegexOptions.Compiled);

  public RectangleF GridPosition { get; set; } = RectangleF.Empty;

  /// <summary>
  /// Optional direct reference to the inventory slot, bypassing the
  /// expensive per-access linear scan in <see cref="Slot"/>.
  /// </summary>
  public NormalInventoryItem DirectSlotRef { get; set; }

  public NormalInventoryItem Slot
  {
    get
    {
      if (DirectSlotRef != null)
        return DirectSlotRef;

      if (GridPosition.Equals(RectangleF.Empty))
      {
        return null;
      }
      var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      var inventoryItems = inventory.VisibleInventoryItems;
      return inventoryItems.First(x => x.GetClientRect().Equals(GridPosition));
    }
  }

  public Entity Item
  {
    get
    {
      if (Slot == null)
      {
        return null;
      }
      return Slot.Item;
    }
  }

  public bool IsIdentified
  {
    get
    {
      if (Item == null)
      {
        return false;
      }
      return Item.GetComponent<ExileCore.PoEMemory.Components.Mods>()?.Identified ?? true;
    }
  }

  public ItemRarity Rarity
  {
    get
    {
      if (Item == null)
      {
        return ItemRarity.Normal;
      }
      return Item.GetComponent<ExileCore.PoEMemory.Components.Mods>()?.ItemRarity ?? ItemRarity.Normal;
    }
  }

  public bool IsCorrupted
  {
    get
    {
      if (Item == null)
      {
        return false;
      }
      return Item.GetComponent<ExileCore.PoEMemory.Components.Base>()?.isCorrupted ?? false;
    }
  }

  public RectangleF Position
  {
    get
    {
      if (Item == null)
      {
        return RectangleF.Empty;
      }
      var position = Slot.GetClientRect();
      return position;
    }
  }

  private Element Tooltip
  {
    get
    {
      if (Slot == null)
      {
        return null;
      }
      if (Slot.Tooltip == null || Slot.Tooltip.ChildCount == 0)
      {
        return null;
      }
      return Slot.Tooltip;
    }
  }

  public bool IsHovered
  {
    get
    {
      return Tooltip != null;
    }
  }

  public ItemData Data
  {
    get
    {
      if (Item == null)
      {
        return null;
      }
      return new ItemData(Item, TujenMem.Instance.GameController);
    }
  }

  public int? Quantity
  {
    get
    {
      if (Item == null)
      {
        return null;
      }
      var tooltip = Tooltip;
      if (tooltip == null)
      {
        return null;
      }
      Element FindNodeWithTerm(Element node)
      {
        if (node.Text != null && node.Text.Contains("Quantity:"))
          return node;

        foreach (var child in node.Children)
        {
          var result = FindNodeWithTerm(child);
          if (result != null)
            return result;
        }

        return null;
      }

      var quantityNode = FindNodeWithTerm(tooltip);
      if (quantityNode == null)
      {
        return 0;
      }
      var match = QuantityTooltipRegex.Match(quantityNode.Text);

      if (match.Success)
      {
        return int.Parse(match.Groups[1].Value);
      }
      else
      {
        return null;
      }
    }

  }

  public int? QuantityFromStats
  {
    get
    {
      if (Item == null) return null;
      var mods = Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
      if (mods?.HumanStats == null) return null;
      foreach (var stat in mods.HumanStats)
      {
        var match = QuantityRegex.Match(stat);
        if (match.Success)
          return int.Parse(match.Groups[1].Value);
      }
      return 0;
    }
  }

  public List<string> Mods
  {
    get
    {
      if (Item == null)
      {
        return new List<string>();
      }
      var mods = Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
      if (mods == null || mods.HumanStats == null)
      {
        Log.Error("Mods is null");
        return new List<string>();
      }
      return mods.HumanStats.Select(x => x.ToLower()).ToList();
    }
  }

  public async SyncTask<bool> Hover()
  {
    await InputAsync.MoveMouseToElement(Position);
    return await InputAsync.Wait();
  }
}
