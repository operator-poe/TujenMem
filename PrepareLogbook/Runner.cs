using System.Linq;
using ExileCore.Shared;
using ExileCore.Shared.Enums;

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
      while (
        l.Quantity < TujenMem.Instance.Settings.PrepareLogbookSettings.MinQuantity
        ||
        l.Mods.Any(entry => badMods.Any(term => entry.Contains(term)))
      )
      {
        // Items should not be magic any more but just in case do a last check (for fractures for example)
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
      }
    }
    return await Stash.CleanUp();
  }

  private static async SyncTask<bool> BlessLogbooks()
  {
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Rare)
      {
        while (!l.IsBlessed)
        {
          await Stash.Blessed.Use(l.Position);
          await InputAsync.WaitX(5);
        }
      }
    }
    await InputAsync.WaitX(5);
    return await Stash.CleanUp();
  }
}