using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using System.Collections;
using System;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;

namespace TujenMem;

public class HaggleProcessWindow
{
  public List<HaggleItem> Items { get; set; } = new();

  private readonly string StatisticsWindowId;

  public HaggleProcessWindow()
  {
    StatisticsWindowId = Statistics.GetWindowId();
  }

  public void ReadItems()
  {
    Log.Debug("Reading available items for haggling");
    Items.Clear();


    Log.Debug($"HaggleWindow.InventoryItems.Count: {TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count}");
    foreach (NormalInventoryItem inventoryItem in TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems)
    {
      var baseItem = TujenMem.Instance.GameController.Files.BaseItemTypes.Translate(inventoryItem.Item.Path);
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
      foreach (var mapping in TujenMem.Instance.Settings.ItemMappings)
      {
        if (mapping.Item1.TrueForAll(x => item.Name.Contains(x) || item.Type.Contains(x)))
        {
          item.Name = mapping.Item2;
        }
      }
      if (item is HaggleItemMap && TujenMem.Instance.Settings.CustomNameForInfluencedMaps != "")
      {
        var map = item as HaggleItemMap;
        if (map.IsInfluenced)
        {
          item.Name = TujenMem.Instance.Settings.CustomNameForInfluencedMaps;
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
        if ((!TujenMem.Instance.Settings.MapsEnabled || map.MapTier < TujenMem.Instance.Settings.MinMapTier) && !map.IsUnique && !map.IsInfluenced)
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

    for (var i = 0; i < TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count; i++)
    {

      var item = Items.Find(x => x.Address == TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Address);
      if (item == null || item.State != HaggleItemState.Unpriced || item.State == HaggleItemState.Priced || item.State == HaggleItemState.TooExpensive)
      {
        continue;
      }

      var attempts = 0;
      while (true)
      {
        attempts++;
        var position = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].GetClientRect().Center;
        Input.SetCursorPos(position);

        yield return new WaitFunctionTimed(() =>
        {
          var ttBody = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip?.GetChildFromIndices(0, 1);
          return TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip != null
            && ttBody != null
            && TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip.GetChildFromIndices(0, 1, ttBody.Children.Count - 1, 1) != null;
        }, false, 1000);
        var ttBody = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip.GetChildFromIndices(0, 1);
        if (TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip == null
          || ttBody == null
          || TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip.GetChildFromIndices(0, 1, ttBody.Children.Count - 1, 1) == null)
        {
          Error.Add("Error while reading tooltip", $"Tooltip structure is unexpected. Item: {item.Name}");
          Error.Add("Tooltip Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip));
          Error.Show();
          yield break;
        }

        var ttHead = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip.GetChildFromIndices(0, 0);
        if (ttHead == null || ttBody == null)
        {
          if (attempts < 3)
          {
            continue;
          }
          Error.Add("Error while reading tooltip", $"Tooltip has no head or body.\nItem: {item.Name}.\nPlease check your hover delay settings and try again.");
          Error.Add("Tooltip Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip));
          Error.Show();
          yield break;
        }
        var ttPriceSection = ttBody.GetChildAtIndex(ttBody.Children.Count - 1);
        if (ttPriceSection == null || ttPriceSection.Children.Count < 2)
        {
          if (attempts < 3)
          {
            continue;
          }
          Error.Add("Error while reading tooltip", $"Tooltip has no price section.\nItem: {item.Name}\nPlease check your hover delay settings and try again.");
          Error.Add("Tooltip Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip));
          Error.Show();
          yield break;
        }
        var ttPriceHead = ttPriceSection.GetChildAtIndex(0);
        var ttPriceBody = ttPriceSection.GetChildAtIndex(1);
        if (ttPriceHead == null || ttPriceBody == null)
        {
          if (attempts < 3)
          {
            continue;
          }
          Error.Add("Error while reading tooltip", $"Tooltip has no price head or body.\nItem: {item.Name}\nPlease check your hover delay settings and try again.");
          Error.Add("Tooltip Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip));
          Error.Show();
          yield break;
        }

        string priceString = ttPriceBody.GetChildAtIndex(0).Text;
        string cleaned = new string(priceString.Where(char.IsDigit).ToArray()).Trim();
        var ttPrice = 0;
        try
        {
          ttPrice = int.Parse(cleaned);
        }
        catch (Exception e)
        {
          Error.Add("Error while reading tooltip", $"Error parsing price: {e}\nText: {priceString}\nCleaned: {cleaned}");
          Error.Add("Tooltip Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.InventoryItems[i].Tooltip));
          Error.Show();
          yield break;
        }

        var ttPriceType = ttPriceBody.GetChildAtIndex(2).Text;

        item.Price = new HaggleCurrency(ttPriceType, ttPrice);
        item.Value = 0;
        NinjaItem ninjaItem = null;
        if (Ninja.Items.ContainsKey(item.Name))
        {
          ninjaItem = Ninja.Items[item.Name].Find(x => x.ChaosValue > 0);
          if (item is HaggleItemGem)
          {
            var gem = item as HaggleItemGem;
            ninjaItem = Ninja.Items[item.Name].Find(x =>
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
            ninjaItem = Ninja.Items[item.Name].Find(x =>
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
        else
        {
          Error.AddAndShow("Error while pricing item", $"There was no equivalent Ninja entry for {item.Name}.\nPlease check your item mappings and your Ninja settings.");
          yield break;
        }

        var itemPrice = item?.Price?.TotalValue() ?? 0;
        if (itemPrice * TujenMem.Instance.Settings.ArtifactValueSettings.ItemPriceMultiplier.Value >= item.Value)
        {
          item.State = HaggleItemState.TooExpensive;
        }
        else
        {
          item.State = HaggleItemState.Priced;
        }


        if (ninjaItem != null)
          items.Add((item, ninjaItem));

        break;
      }
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
      yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
      Input.Click(MouseButtons.Left);

      yield return new WaitFunctionTimed(() => TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow is { IsVisible: true }, false, 500, "HaggleWindow not visible");
      if (TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow is { IsVisible: false })
      {
        Error.AddAndShow("Error while haggling", $"HaggleWindow not visible.\nA click on an item has not resulted in the HaggleWindow being visible.\nItem: {item.Name}.\nPlease check your hover delay settings and try again.");
        yield break;
      }

      var attempts = 0;
      while (TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow is { IsVisible: true })
      {
        attempts++;
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
        var maxOffer = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow.ArtifactOfferSliderElement.CurrentMaxOffer;
        var minOffer = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow.ArtifactOfferSliderElement.CurrentMinOffer;
        var currentOffer = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow.ArtifactOfferSliderElement.CurrentOffer;

        var multiplier = attempts == 1 ? TujenMem.Instance.Settings.HaggleMultiplierSettings.Try1 : attempts == 2 ? TujenMem.Instance.Settings.HaggleMultiplierSettings.Try2 : TujenMem.Instance.Settings.HaggleMultiplierSettings.Try3;
        int targetOffer = TujenMem.Instance.Settings.HaggleMultiplierSettings.MultiplierMode == "Min To Max" ? (int)(minOffer + Math.Ceiling((maxOffer - minOffer) * multiplier)) : (int)(maxOffer * multiplier);
        while (currentOffer > targetOffer && currentOffer > minOffer)
        {
          if (TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow is { IsVisible: false } || attempts > 3)
          {
            break;
          }
          Input.VerticalScroll(false, attempts <= 1 ? 10 : 1);
          yield return new WaitTime(0);
          currentOffer = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow.ArtifactOfferSliderElement.CurrentOffer;
        }
        yield return new WaitTime(0);
        Input.SetCursorPos(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow.ConfirmButton.GetClientRect().Center);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay * 10);
      }
    }
    Log.Debug("Finished haggling for items");
    yield return true;
  }

  private bool IsBlacklisted(HaggleItem item)
  {
    foreach (string black in TujenMem.Instance.Settings.Blacklist)
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
    foreach (string white in TujenMem.Instance.Settings.Whitelist)
    {
      if (item.Name.Contains(white) || item.Type.Contains(white))
      {
        return true;
      }
    }
    return false;
  }

}
