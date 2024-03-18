using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace TujenMem.PrepareLogbook;

public class BlessValue
{
  public bool Enabled { get; set; }
  public string NameContains { get; set; }
  public int MinValue { get; set; }
}

public class Logbook
{
  public Vector2 GridPosition { get; set; } = Vector2.Zero;

  private List<string> AreaOrder;
  private List<string> FactionOrder;
  private Dictionary<string, BlessValue> BlessValues { get; set; }

  public Logbook()
  {
    AreaOrder = TujenMem.Instance.Settings.PrepareLogbookSettings.AreaOrder;
    FactionOrder = TujenMem.Instance.Settings.PrepareLogbookSettings.FactionOrder;

    BlessValues = new Dictionary<string, BlessValue>
    {
        {
            "MapExpeditionMaximumPlacementDistance",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_ExplosiveRange.Value,
                NameContains = "MapExpeditionMaximumPlacementDistance",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_ExplosiveRange_Min.Value
            }
        },
        {
            "MapExpeditionExplosives",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfExplosives.Value,
                NameContains = "MapExpeditionExplosives",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfExplosives_Min.Value
            }
        },
        {
            "MapExpeditionExplosionRadius",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_ExplosiveRadius.Value,
                NameContains = "MapExpeditionExplosionRadius",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_ExplosiveRadius_Min.Value
            }
        },
        {
            "MapExpeditionChestCount",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_AdditionalChests.Value,
                NameContains = "MapExpeditionChestCount",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_AdditionalChests_Min.Value
            }
        },
        {
            "MapExpeditionArtifactQuantity",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_QuantityOfArtifactsByMonsters.Value,
                NameContains = "MapExpeditionArtifactQuantity",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_QuantityOfArtifactsByMonsters_Min.Value
            }
        },
        {
            "MapExpeditionEliteMonsterQuantity",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfMonsterMarkers.Value,
                NameContains = "MapExpeditionEliteMonsterQuantity",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfMonsterMarkers_Min.Value
            }
        },
        {
            "MapExpeditionRelics",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfRemnants.Value,
                NameContains = "MapExpeditionRelics",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_NumberOfRemnants_Min.Value
            }
        },
        {
            "MapExpeditionExtraRelicSuffixChance",
            new BlessValue
            {
                Enabled = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_AdditionalSuffixMod.Value,
                NameContains = "MapExpeditionExtraRelicSuffixChance",
                MinValue = TujenMem.Instance.Settings.PrepareLogbookSettings.Bless_AdditionalSuffixMod_Min.Value
            }
        }
    };
  }

  private NormalInventoryItem Slot
  {
    get
    {
      if (GridPosition.Equals(Vector2.Zero))
      {
        return null;
      }
      var inventory = TujenMem.Instance.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
      var inventoryItems = inventory.VisibleInventoryItems;
      return inventoryItems.First(x => x.GetClientRect().Center.Equals(GridPosition));
    }
  }

  private Entity Item
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

  public Vector2 Position
  {
    get
    {
      if (Item == null)
      {
        return Vector2.Zero;
      }
      var position = Slot.GetClientRect().Center;
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
      if (Slot.Tooltip?.GetChildAtIndex(0).ChildCount == 0)
      {
        return null;
      }
      return Slot.Tooltip;
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
      var match = Regex.Match(quantityNode.Text, @"\+(\d+)%");

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

  public List<string> Mods
  {
    get
    {
      if (Item == null)
      {
        return null;
      }
      var mods = Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
      if (mods == null || mods.HumanStats == null)
      {
        return null;
      }
      return mods.HumanStats.Select(x => x.ToLower()).ToList();
    }
  }

  public ExpeditionAreaData Area
  {
    get
    {
      if (Item == null)
      {
        return null;
      }
      var expeditionSaga = Item.GetComponent<ExileCore.PoEMemory.Components.ExpeditionSaga>();
      if (expeditionSaga == null)
      {
        return null;
      }
      var areas = expeditionSaga.Areas;
      areas.Sort((node1, node2) =>
      {
        int index1Array1 = FactionOrder.IndexOf(node1.Faction);
        int index2Array1 = FactionOrder.IndexOf(node2.Faction);
        int index1Array2 = AreaOrder.IndexOf(node1.Name);
        int index2Array2 = AreaOrder.IndexOf(node2.Name);

        index1Array1 = index1Array1 == -1 ? int.MaxValue : index1Array1;
        index2Array1 = index2Array1 == -1 ? int.MaxValue : index2Array1;

        int comparison = index1Array1.CompareTo(index2Array1);
        if (comparison == 0)
        {
          return index1Array2.CompareTo(index2Array2);
        }

        return comparison;
      });
      return areas[0];
    }
  }

  public bool IsBlessed
  {
    get
    {
      if (Item == null)
      {
        return false;
      }
      var area = Area;
      var modBlessedOk = true;
      foreach (var mod in area.ImplicitMods)
      {
        if (BlessValues.ContainsKey(mod.Group))
        {
          if (!BlessValues[mod.Group].Enabled)
          {
            continue;
          }
          if (mod.Values[0] < BlessValues[mod.Group].MinValue)
          {
            modBlessedOk = false;
            break;
          }
        }
      }

      return modBlessedOk;
    }
  }

  public IEnumerator Hover()
  {
    Input.SetCursorPos(Position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
  }
}