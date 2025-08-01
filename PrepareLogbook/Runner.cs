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

  private static PStash Stash { get; set; }
  private static Inventory Inventory { get; set; }

  public static async SyncTask<bool> RollAndBlessLogbooksCoroutine()
  {
    Log.Debug("Starting RollAndBlessLogbooksCoroutine");
    Stash = new PStash();
    Inventory = new Inventory();

    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableRolling)
    {
      Log.Debug("Rolling is enabled, starting roll process");
      await RollLogbooks();
      Log.Debug("Roll process completed");
    }
    else
    {
      Log.Debug("Rolling is disabled, skipping roll process");
    }

    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableBlessing)
    {
      Log.Debug("Blessing is enabled, starting bless process");
      await BlessLogbooks();
      Log.Debug("Bless process completed");
    }
    else
    {
      Log.Debug("Blessing is disabled, skipping bless process");
    }

    Log.Debug("RollAndBlessLogbooksCoroutine completed");
    return await Stash.CleanUp();
  }

  public static async SyncTask<bool> IdentifyItemsInStash()
  {
    Log.Debug("Instantiating Stash");
    Stash = new PStash(false);
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
    Log.Debug("Starting logbook rolling process");

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
    var alchOrb = Stash.Binding.StackSize > Stash.Alchemy.StackSize ? Stash.Binding : Stash.Alchemy;
    await Stash.CleanUp();

    // Actually roll the logbooks
    var badMods = TujenMem.Instance.Settings.PrepareLogbookSettings.ModsBlackList.Value.Split(',').Select(x => x.ToLower()).ToList();

    if (TujenMem.Instance.Settings.PrepareLogbookSettings.UseChaos)
    {
      Log.Debug("Using chaos orbs for rolling");
      // Use chaos orbs - process each logbook individually
      foreach (var l in Inventory.Logbooks)
      {
        Log.Debug($"Processing logbook: {l.Item.RenderName}");
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

          await Stash.Chaos.Use(l.Position);
          await InputAsync.WaitX(5);

        } while (ShouldContinueProcessing(l, badMods));
      }
    }
    else
    {
      Log.Debug("Using alch/scour batching for rolling");
      // Use alch/scour in batches for efficiency
      bool needsMorePasses = true;
      int passCount = 0;
      const int maxPasses = 50; // Safety limit to prevent infinite loops

      while (needsMorePasses && passCount < maxPasses)
      {
        passCount++;
        needsMorePasses = false;

        // Hover over any un-hovered logbooks to update their tooltips before processing
        try
        {
          foreach (var l in Inventory.Logbooks)
          {
            if (l == null || l.IsCorrupted || l.IsHovered) continue;
            await l.Hover();
          }
        }
        catch (Exception e)
        {
          Log.Error($"Error hovering over logbook: {e.Message}");
          Log.Error($"Error hovering over logbook: {e.StackTrace}");
        }

        // Get all logbooks that need processing
        var logbooksToProcess = Inventory.Logbooks
          .Where(l => l != null && !l.IsCorrupted && ShouldContinueProcessing(l, badMods))
          .ToList();

        Log.Debug($"Pass {passCount}: Found {logbooksToProcess.Count} logbooks that need processing");

        if (logbooksToProcess.Count == 0)
        {
          Log.Debug("No logbooks need processing, stopping rolling");
          break;
        }

        // Batch scour only logbooks that are magic or rare (need to be reset to normal)
        var logbooksToScour = logbooksToProcess
          .Where(l => l.Rarity == ItemRarity.Magic || l.Rarity == ItemRarity.Rare)
          .ToList();

        if (logbooksToScour.Count > 0)
        {
          Log.Debug($"Scouring {logbooksToScour.Count} logbooks");
          await Stash.Scouring.Hold();
          foreach (var l in logbooksToScour)
          {
            await Stash.Scouring.Use(l.Position);
            await InputAsync.WaitX(2); // Reduced wait time for batch processing
          }
          await Stash.Scouring.Release();
          await InputAsync.WaitX(5);
        }

        // Batch alch only logbooks that are normal rarity (either were normal or just scoured)
        var logbooksToAlch = logbooksToProcess
          .Where(l => l.Rarity == ItemRarity.Normal)
          .ToList();

        if (logbooksToAlch.Count > 0)
        {
          Log.Debug($"Alching {logbooksToAlch.Count} logbooks");
          await alchOrb.Hold();
          foreach (var l in logbooksToAlch)
          {
            await alchOrb.Use(l.Position);
            await InputAsync.WaitX(2); // Reduced wait time for batch processing
          }
          await alchOrb.Release();
          await InputAsync.WaitX(5);
        }

        // Check if any logbooks still need processing
        needsMorePasses = Inventory.Logbooks
          .Any(l => !l.IsCorrupted && ShouldContinueProcessing(l, badMods));
      }

      if (passCount >= maxPasses)
      {
        Log.Warning($"Stopped logbook rolling after {maxPasses} passes to prevent infinite loop");
      }
    }

    Log.Debug("Logbook rolling process completed");
    return await Stash.CleanUp();
  }

  private static bool ShouldContinueProcessing(ReRollable l, List<string> badMods)
  {
    var settings = TujenMem.Instance.Settings.PrepareLogbookSettings;
    var mapInfo = l.Data.MapInfo;

    bool isQuantityTooLow = (l.Quantity ?? 0) < settings.MinQuantity;
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