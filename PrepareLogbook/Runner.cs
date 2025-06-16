using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ItemFilterLibrary;
using SharpDX;

namespace TujenMem.PrepareLogbook;

public class Runner
{
  public static string CoroutineNameRollAndBless { get; set; } = "TujenMem_PrepareLogbook_RollAndBless";

  private static Stash Stash { get; set; }
  private static Inventory Inventory { get; set; }

  public static async SyncTask<bool> RollAndBlessLogbooksCoroutine()
  {
    Stash = new Stash();
    Inventory = new Inventory();

    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableRolling)
    {
      await RollLogbooks();
    }
    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableBlessing)
    {
      await BlessLogbooks();
    }

    return await Stash.CleanUp();
  }

  public static async SyncTask<bool> IdentifyItemsInStash()
  {
    Log.Debug("Instantiating Stash");
    Stash = new Stash(false);
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
    var unidentifiedUniques = Stash.GetUnidentifiedUniques();
    if (unidentifiedUniques.Count == 0) return false;
    await scroll.Hold();
    foreach (var unique in unidentifiedUniques)
    {
      await InputAsync.ClickElement(unique.GetClientRect(), System.Windows.Forms.MouseButtons.Left);
      await InputAsync.WaitX(5);
    }
    await scroll.Release();

    return await Stash.CleanUp();
  }

  private static async SyncTask<bool> RollLogbooks()
  {
    // Identify all logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (!l.IsIdentified)
      {
        await Stash.Wisdom.Use(l.Position);
      }
    }
    await Stash.CleanUp();

    // Scour magic logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Magic)
      {
        await Stash.Scouring.Use(l.Position);
        await InputAsync.WaitX(5);
      }
    }
    await Stash.CleanUp();

    // Apply chisels to maps
    if (TujenMem.Instance.Settings.PrepareLogbookSettings.UseChiselsForMaps)
    {
      foreach (var l in Inventory.Logbooks)
      {
        if (l.IsCorrupted) continue;
        if (l is Map map && map.Quality < 20)
        {
          double needed = (20 - map.Quality) / 5;
          needed = Math.Max(Math.Ceiling(needed), 1);
          for (int i = 0; i < needed; i++)
          {
            await Stash.Chisel.Use(l.Position);
          }
          await InputAsync.WaitX(5);
        }
      }
      await Stash.CleanUp();
    }

    // Alch normal logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Normal)
      {
        await Stash.Alchemy.Use(l.Position);
        await InputAsync.WaitX(5);
      }
    }
    await Stash.CleanUp();

    // Actually roll the logbooks
    var badMods = TujenMem.Instance.Settings.PrepareLogbookSettings.ModsBlackList.Value.Split(',').Select(x => x.ToLower()).ToList();
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      await l.Hover();

      if (!ShouldContinueProcessing(l, badMods)) continue;
      do
      {
        // Items should not be magic anymore but just in case do a last check (for fractures for example)
        if (l.Rarity == ItemRarity.Magic)
        {
          await Stash.Regal.Use(l.Position);
          await Stash.Regal.Release();
        }

        if (TujenMem.Instance.Settings.PrepareLogbookSettings.UseChaos)
        {
          await Stash.Chaos.Use(l.Position);
        }
        else
        {
          await Stash.Scouring.Use(l.Position);
          await Stash.Scouring.Release();
          await Stash.Alchemy.Use(l.Position);
          await Stash.Alchemy.Release();
        }

        await InputAsync.WaitX(5);

      } while (ShouldContinueProcessing(l, badMods));

      //       while (
      //     l.Quantity < TujenMem.Instance.Settings.PrepareLogbookSettings.MinQuantity
      //     ||
      //     l.Mods.Any(entry => badMods.Any(term => entry.Contains(term)))
      //     ||
      //     (
      //         (TujenMem.Instance.Settings.PrepareLogbookSettings.MinScarabsT17 >= 1 && l.Data.MapInfo.Tier >= 17 && l.Data.MapInfo.MoreScarabs < TujenMem.Instance.Settings.PrepareLogbookSettings.MinScarabsT17)
      //         &&
      //         (TujenMem.Instance.Settings.PrepareLogbookSettings.MinMapsT17 >= 1 && l.Data.MapInfo.Tier >= 17 && l.Data.MapInfo.MoreMaps < TujenMem.Instance.Settings.PrepareLogbookSettings.MinMapsT17)
      //         &&
      //         (TujenMem.Instance.Settings.PrepareLogbookSettings.MinCurrencyT17 >= 1 && l.Data.MapInfo.Tier >= 17 && l.Data.MapInfo.MoreCurrency < TujenMem.Instance.Settings.PrepareLogbookSettings.MinCurrencyT17)
      //     )
      // )
      //       {
      //         // Items should not be magic any more but just in case do a last check (for fractures for example)
      //         if (l.Rarity == ItemRarity.Magic)
      //         {
      //           await Stash.Regal.Use(l.Position);
      //           await Stash.Regal.Release();
      //         }
      //         if (TujenMem.Instance.Settings.PrepareLogbookSettings.UseChaos)
      //         {
      //           await Stash.Chaos.Use(l.Position);
      //         }
      //         else
      //         {
      //           await Stash.Scouring.Use(l.Position);
      //           await Stash.Scouring.Release();
      //           await Stash.Alchemy.Use(l.Position);
      //           await Stash.Alchemy.Release();
      //         }
      //         await InputAsync.WaitX(5);
      //       }
    }
    return await Stash.CleanUp();
  }

  private static bool ShouldContinueProcessing(ReRollable l, List<string> badMods)
  {
    var settings = TujenMem.Instance.Settings.PrepareLogbookSettings;
    var mapInfo = l.Data.MapInfo;

    bool isQuantityTooLow = l.Quantity < settings.MinQuantity;
    bool hasBadMods = l.Mods.Any(entry => badMods.Any(term => entry.Contains(term)));

    if (isQuantityTooLow || hasBadMods)
    {
      return true;
    }

    bool isHighTier = mapInfo.Tier >= 17;

    if (!isHighTier)
    {
      return false;
    }

    bool shouldCheckScarabs = settings.MinScarabsT17 >= 1;
    bool shouldCheckMaps = settings.MinMapsT17 >= 1;
    bool shouldCheckCurrency = settings.MinCurrencyT17 >= 1;
    bool shouldCheckPackSize = settings.MinPackSizeT17 >= 1;

    bool hasEnoughScarabs = mapInfo.MoreScarabs > settings.MinScarabsT17;
    bool hasEnoughMaps = mapInfo.MoreMaps > settings.MinMapsT17;
    bool hasEnoughCurrency = mapInfo.MoreCurrency > settings.MinCurrencyT17;
    bool hasEnoughPackSize = mapInfo.PackSize > settings.MinPackSizeT17;

    if (settings.MinT17OrMode)
    {
      if (shouldCheckScarabs && hasEnoughScarabs) return false;
      if (shouldCheckMaps && hasEnoughMaps) return false;
      if (shouldCheckCurrency && hasEnoughCurrency) return false;
      if (shouldCheckPackSize && hasEnoughPackSize) return false;
    }

    if (
      (shouldCheckScarabs && !hasEnoughScarabs) ||
      (shouldCheckMaps && !hasEnoughMaps) ||
      (shouldCheckCurrency && !hasEnoughCurrency) ||
       (shouldCheckPackSize && !hasEnoughPackSize)
    ) return true;

    return false;

  }

  private static async SyncTask<bool> BlessLogbooks()
  {
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Rare && l is Logbook logbook)
      {
        while (!logbook.IsBlessed)
        {
          await Stash.Blessed.Use(logbook.Position);
          await InputAsync.WaitX(5);
        }
      }
    }
    await InputAsync.WaitX(5);
    return await Stash.CleanUp();
  }
}