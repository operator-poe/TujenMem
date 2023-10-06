using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements.ExpeditionElements;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;

namespace TujenMem;

public class HaggleProcessWindow
{
  private GameController GameController { get; set; }
  private ExpeditionVendorElement HaggleWindow;
  public List<HaggleItem> Items { get; set; } = new();

  private readonly Dictionary<string, List<NinjaItem>> _ninjaItems = new();

  private TujenMemSettings Settings;

  private readonly string StatisticsWindowId;

  public HaggleProcessWindow(ExpeditionVendorElement haggleWindow, GameController gameController, TujenMemSettings settings, Dictionary<string, List<NinjaItem>> ninjaItems)
  {
    GameController = gameController;
    HaggleWindow = haggleWindow;
    Settings = settings;
    _ninjaItems = ninjaItems;
    StatisticsWindowId = Statistics.GetWindowId();
  }

  public void ReadItems()
  {
    Log.Debug("Reading available items for haggling");
    Items.Clear();

    Log.Debug($"HaggleWindow.InventoryItems.Count: {HaggleWindow.InventoryItems.Count}");
    foreach (NormalInventoryItem inventoryItem in HaggleWindow.InventoryItems)
    {
      var baseItem = GameController.Files.BaseItemTypes.Translate(inventoryItem.Item.Path);
      var stack = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>();
      var type = baseItem.ClassName;
      var address = inventoryItem.Address;

      if (type == "Map")
      {
        var map = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Map>();
        var mods = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        var tier = map.Tier;
        var isUnique = mods.ItemRarity == ExileCore.Shared.Enums.ItemRarity.Unique;
        var isInfluenced = false;

        if (mods != null && mods.ItemMods != null)
        {
          foreach (var mod in mods.ItemMods)
          {
            if (mod.Name.Contains("Elder") || mod.Name.Contains("Shaper") || mod.Name.Contains("Conqueror"))
            {
              isInfluenced = true;
              break;
            }
          }
        }

        Items.Add(new HaggleItemMap
        {
          Address = address,
          Position = inventoryItem.GetClientRect().Center,
          Name = isUnique ? mods.UniqueName : baseItem.BaseName,
          Type = type,
          Amount = stack?.Size ?? 1,
          Value = 0,
          Price = null,
          MapTier = tier,
          IsUnique = isUnique,
          IsInfluenced = isInfluenced
        });

        continue;
      }
      else if (baseItem.BaseName.Contains("Cluster"))
      {
        var baseCluster = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Base>();
        var mods = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        var itemLevel = mods.ItemLevel;
        var passives = int.Parse(Regex.Replace(mods.EnchantedStats[0], "[^0-9]", ""));
        var name = mods.EnchantedStats[2].Replace("Added Small Passive Skills grant: ", "").Trim();

        Items.Add(new HaggleItemClusterJewel
        {
          Address = address,
          Position = inventoryItem.GetClientRect().Center,
          Name = name,
          Type = type,
          Amount = stack?.Size ?? 1,
          Value = 0,
          Price = null,
          ItemLevel = itemLevel,
          PassiveSkills = passives,
          BaseType = baseCluster.Name
        });

      }
      else if (type.Contains("Gem"))
      {
        var gem = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.SkillGem>();
        var quality = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Quality>();
        var gemBase = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.Base>();
        var level = gem.Level;
        var qual = quality.ItemQuality;

        Items.Add(new HaggleItemGem
        {
          Address = address,
          Position = inventoryItem.GetClientRect().Center,
          Name = baseItem.BaseName,
          Type = type,
          Amount = stack?.Size ?? 1,
          Value = 0,
          Price = null,
          Level = level,
          Quality = qual,
          Corrupted = gemBase.isCorrupted,
        });

        continue;
      }
      else
      {
        var addToType = "";
        if (baseItem.ClassName.Contains("Contract"))
        {
          var contract = inventoryItem.Item.GetComponent<ExileCore.PoEMemory.Components.HeistContract>();
          addToType = contract.RequiredJob.Name;
        }


        Items.Add(new HaggleItem
        {
          Address = address,
          Position = inventoryItem.GetClientRect().Center,
          Name = baseItem.BaseName,
          Type = baseItem.ClassName + addToType,
          Amount = stack?.Size ?? 1,
          Value = 0,
          Price = null
        });
      }
    }
    Log.Debug("Finished reading items for haggling");
  }

  public void ApplyMappingToItems()
  {
    Log.Debug("Applying mapping to items");
    foreach (HaggleItem item in Items)
    {
      foreach (var mapping in Settings.ItemMappings)
      {
        if (mapping.Item1.TrueForAll(x => item.Name.Contains(x) || item.Type.Contains(x)))
        {
          item.Name = mapping.Item2;
        }
      }
      if (item is HaggleItemMap && Settings.CustomNameForInfluencedMaps != "")
      {
        var map = item as HaggleItemMap;
        if (map.IsInfluenced)
        {
          item.Name = Settings.CustomNameForInfluencedMaps;
        }
      }
    }
  }

  public void FilterItems()
  {
    Log.Debug("Filtering items");
    foreach (HaggleItem item in Items)
    {
      var white = IsWhitelisted(item);

      if (white)
      {
        item.State = HaggleItemState.Unpriced;
        continue;
      }

      var black = IsBlacklisted(item);


      if (item is HaggleItemMap)
      {
        var map = item as HaggleItemMap;
        if ((!Settings.MapsEnabled || map.MapTier < Settings.MinMapTier) && !map.IsUnique && !map.IsInfluenced)
        {
          item.State = HaggleItemState.Rejected;
          continue;
        }
      }
      if (black)
      {
        item.State = HaggleItemState.Rejected;
        continue;
      }
      item.State = HaggleItemState.Unpriced;
    }
  }

  public IEnumerator GetItemPrices()
  {
    Log.Debug("Getting item prices");
    List<(HaggleItem, NinjaItem)> items = new();

    foreach (NormalInventoryItem inventoryItem in HaggleWindow.InventoryItems)
    {
      var item = Items.Find(x => x.Address == inventoryItem.Address);
      if (item == null || item.State != HaggleItemState.Unpriced || item.State == HaggleItemState.Priced || item.State == HaggleItemState.TooExpensive)
      {
        continue;
      }

      var position = inventoryItem.GetClientRect().Center;
      Input.SetCursorPos(position);

      var tt = inventoryItem.Tooltip;
      yield return new WaitFunctionTimed(() => tt.Children.Count > 0 && tt.Children[0].Children.Count > 0, true, 1000, "Price Tooltip TIMEOUT");
      if (tt.Children.Count == 0 || tt.Children[0].Children.Count == 0)
      {
        continue;
      }

      var ttMain = tt.Children[0];
      var ttHead = ttMain.Children[0];
      var ttBody = ttMain.Children[1];
      var ttPriceSection = ttBody.Children[ttBody.Children.Count - 1];
      var ttPriceHead = ttPriceSection.Children[0];
      var ttPriceBody = ttPriceSection.Children[1];

      string lesserString = ttPriceBody.Children[0].Text;
      string cleaned = new string(lesserString.Where(char.IsDigit).ToArray()).Trim();
      var ttPrice = int.Parse(cleaned);

      var ttPriceType = ttPriceBody.Children[2].Text;

      item.Price = new HaggleCurrency(ttPriceType, ttPrice, Settings);
      item.Value = 0;
      NinjaItem ninjaItem = null;
      if (_ninjaItems.ContainsKey(item.Name))
      {
        ninjaItem = _ninjaItems[item.Name].Find(x => x.ChaosValue > 0);
        if (item is HaggleItemGem)
        {
          var gem = item as HaggleItemGem;
          ninjaItem = _ninjaItems[item.Name].Find(x =>
          {
            if (x is NinjaItemGem)
            {
              var ninjaGem = x as NinjaItemGem;
              return ninjaGem.ChaosValue > 10
                && ninjaGem.Level == gem.Level
                && gem.Corrupted == ninjaGem.Corrupted
                && (gem.Level > 1 || ninjaGem.SpecialSupport)
                && (ninjaGem.Quality == gem.Quality || ninjaGem.SpecialSupport);
            }
            return false;
          }
          );
        }
        else if (item is HaggleItemClusterJewel)
        {
          var cluster = item as HaggleItemClusterJewel;
          ninjaItem = _ninjaItems[item.Name].Find(x =>
          {
            if (x is NinjaItemClusterJewel)
            {
              var ninjaCluster = x as NinjaItemClusterJewel;
              return ninjaCluster.ChaosValue > 10
                && ninjaCluster.ItemLevel == cluster.ItemLevel
                && ninjaCluster.PassiveSkills == cluster.PassiveSkills;
            }
            return false;
          }
          );
        }
        if (ninjaItem != null)
        {
          item.Value = ninjaItem.ChaosValue * item.Amount;
        }
      }

      var itemPrice = item?.Price?.TotalValue() ?? 0;
      if (itemPrice * 0.7f >= item.Value)
      {
        item.State = HaggleItemState.TooExpensive;
      }
      else
      {
        item.State = HaggleItemState.Priced;
      }

      if (ninjaItem != null)
        items.Add((item, ninjaItem));
    }
    Log.Debug($"Finished getting item prices. {items.Count} items priced.");
    if (TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableStatistics)
    {
      foreach (var (item, ninjaItem) in items)
        Statistics.RecordItem(StatisticsWindowId, ninjaItem, item);
    }
  }

  public IEnumerator HaggleForItems()
  {
    Log.Debug("Haggling for items");
    foreach (HaggleItem item in Items)
    {
      if (item.State != HaggleItemState.Priced)
      {
        continue;
      }

      var position = item.Position;
      Input.SetCursorPos(position);
      yield return new WaitTime(Settings.HoverItemDelay);
      Input.Click(MouseButtons.Left);

      var haggleWindow = HaggleWindow.TujenHaggleWindow;

      yield return new WaitFunctionTimed(() => haggleWindow is { IsVisible: true }, true, 500, "HaggleWindow not visible");
      if (haggleWindow is { IsVisible: false })
      {
        Log.Error("HaggleWindow not visible");
        var routine = Core.ParallelRunner.FindByName("TujenMem_Haggle");
        routine?.Done();
        continue;
      }

      var attempts = 0;
      while (haggleWindow is { IsVisible: true })
      {
        attempts++;
        yield return new WaitTime(Settings.HoverItemDelay);
        var maxOffer = haggleWindow.ArtifactOfferSliderElement.CurrentMaxOffer;
        var minOffer = haggleWindow.ArtifactOfferSliderElement.CurrentMinOffer;
        var currentOffer = haggleWindow.ArtifactOfferSliderElement.CurrentOffer;

        var multiplier = attempts == 1 ? Settings.HaggleMultiplierSettings.Try1 : attempts == 2 ? Settings.HaggleMultiplierSettings.Try2 : Settings.HaggleMultiplierSettings.Try3;
        while (currentOffer > maxOffer * multiplier && currentOffer > minOffer)
        {
          if (haggleWindow is { IsVisible: false } || attempts > 3)
          {
            break;
          }
          Input.VerticalScroll(false, attempts <= 1 ? 10 : 1);
          yield return new WaitTime(0);
          currentOffer = haggleWindow.ArtifactOfferSliderElement.CurrentOffer;
        }
        yield return new WaitTime(0);
        Input.SetCursorPos(haggleWindow.ConfirmButton.GetClientRect().Center);
        yield return new WaitTime(Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(Settings.HoverItemDelay * 10);
      }
    }
    Log.Debug("Finished haggling for items");
    yield return true;
  }

  private bool IsBlacklisted(HaggleItem item)
  {
    foreach (string black in Settings.Blacklist)
    {
      if (item.Name.Contains(black) || item.Type.Contains(black))
      {
        return true;
      }
    }
    return false;
  }

  private bool IsWhitelisted(HaggleItem item)
  {
    foreach (string white in Settings.Whitelist)
    {
      if (item.Name.Contains(white) || item.Type.Contains(white))
      {
        return true;
      }
    }
    return false;
  }

}
