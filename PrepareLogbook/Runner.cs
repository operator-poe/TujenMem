using System.Collections;
using System.Linq;
using ExileCore.Shared;
using ExileCore.Shared.Enums;

namespace TujenMem.PrepareLogbook;

public class Runner
{
  public static string CoroutineNameRollAndBless { get; set; } = "TujenMem_PrepareLogbook_RollAndBless";

  private static Stash Stash { get; set; }
  private static Inventory Inventory { get; set; }

  public static IEnumerator RollAndBlessLogbooksCoroutine()
  {
    Stash = new Stash();
    Inventory = new Inventory();

    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableRolling)
    {
      yield return RollLogbooks();
    }
    if (TujenMem.Instance.Settings.PrepareLogbookSettings.EnableBlessing)
    {
      yield return BlessLogbooks();
    }

    Stash.CleanUp();
  }

  private static IEnumerator RollLogbooks()
  {
    // Identify all logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (!l.IsIdentified)
      {
        yield return Stash.Wisdom.Use(l.Position);
      }
    }
    yield return Stash.CleanUp();

    // Scour magic logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Magic)
      {
        yield return Stash.Scouring.Use(l.Position);
        yield return new WaitTime(80);
      }
    }
    yield return Stash.CleanUp();

    // Alch normal logbooks
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Normal)
      {
        yield return Stash.Alchemy.Use(l.Position);
        yield return new WaitTime(80);
      }
    }
    yield return Stash.CleanUp();

    // Actually roll the logbooks
    var badMods = TujenMem.Instance.Settings.PrepareLogbookSettings.ModsBlackList.Value.Split(',').Select(x => x.ToLower()).ToList();
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      yield return l.Hover();
      while (
        l.Quantity < TujenMem.Instance.Settings.PrepareLogbookSettings.MinQuantity
        ||
        l.Mods.Any(entry => badMods.Any(term => entry.Contains(term)))
      )
      {
        // Items should not be magic any more but just in case do a last check (for fractures for example)
        if (l.Rarity == ItemRarity.Magic)
        {
          yield return Stash.Regal.Use(l.Position);
          yield return Stash.Regal.Release();
        }
        if (TujenMem.Instance.Settings.PrepareLogbookSettings.UseChaos)
        {
          yield return Stash.Chaos.Use(l.Position);
        }
        else
        {
          yield return Stash.Scouring.Use(l.Position);
          yield return Stash.Scouring.Release();
          yield return Stash.Alchemy.Use(l.Position);
          yield return Stash.Alchemy.Release();
        }
        yield return new WaitTime(80);
      }
    }
    yield return Stash.CleanUp();
  }

  private static IEnumerator BlessLogbooks()
  {
    foreach (var l in Inventory.Logbooks)
    {
      if (l.IsCorrupted) continue;
      if (l.Rarity == ItemRarity.Rare)
      {
        while (!l.IsBlessed)
        {
          yield return Stash.Blessed.Use(l.Position);
          yield return new WaitTime(80);
        }
      }
    }
    yield return new WaitTime(80);
    yield return Stash.CleanUp();
  }
}