using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;

namespace TujenMem.ItemRoller;

public class RollerStash
{
  private readonly HashSet<string> _currencyNames;

  public Dictionary<string, Currency> Currencies { get; set; } = new Dictionary<string, Currency>();

  public RollerStash(IEnumerable<string> requiredCurrencies, bool refresh = true)
  {
    _currencyNames = new HashSet<string>(requiredCurrencies);
    if (refresh)
    {
      RefreshCurrencies();
    }
  }

  public Currency Get(string name)
  {
    return Currencies.TryGetValue(name, out var c) ? c : null;
  }

  public Currency GetBestAlternative(string primary, string alternative)
  {
    var p = Get(primary);
    var a = Get(alternative);
    if (p == null) return a;
    if (a == null) return p;
    return a.StackSize > p.StackSize ? a : p;
  }

  public async SyncTask<bool> CleanUp()
  {
    foreach (var currency in Currencies.Values)
    {
      if (currency.Holding)
      {
        await currency.Release();
      }
    }
    InputAsync.LOCK_CONTROLLER = false;
    InputAsync.IControllerEnd();
    return await InputAsync.Wait();
  }

  public void RefreshCurrencies()
  {
    var stash = TujenMem.Instance.GameController.IngameState.IngameUi.StashElement;
    if (stash == null || !stash.IsVisible)
    {
      DebugWindow.LogError("Stash is not open");
    }

    foreach (var currencyName in _currencyNames)
    {
      if (!Currencies.ContainsKey(currencyName))
      {
        var currency = new Currency
        {
          Name = currencyName,
        };
        Currencies[currencyName] = currency;
      }
      if (Currencies[currencyName].ChildIndex == -1)
      {
        for (int i = 0; i < (stash?.VisibleStash?.VisibleInventoryItems?.Count ?? 0); i++)
        {
          var item = stash.VisibleStash.VisibleInventoryItems[i];
          var itemName = item.Item.GetComponent<ExileCore.PoEMemory.Components.Base>()?.Name;
          if (currencyName == itemName)
          {
            Currencies[currencyName].ChildIndex = i;
            break;
          }
        }
      }
    }
  }

  public List<NormalInventoryItem> GetUnidentifiedUniques()
  {
    var stash = TujenMem.Instance.GameController.IngameState.IngameUi.StashElement;
    if (stash == null || !stash.IsVisible)
    {
      DebugWindow.LogError("Stash is not open");
    }

    var unidentifiedUniques = new List<NormalInventoryItem>();
    Log.Debug("Parsing stash");
    var cnt = stash?.VisibleStash?.VisibleInventoryItems?.Count ?? 0;
    for (int i = 0; i < cnt; i++)
    {
      var Item = stash.VisibleStash.VisibleInventoryItems[i];
      Log.Debug("Parsing item " + i + "/" + cnt);

      var id = Item.Item.GetComponent<ExileCore.PoEMemory.Components.Mods>()?.Identified ?? true;
      if (!id)
      {
        unidentifiedUniques.Add(Item);
      }
    }
    unidentifiedUniques.Sort((a, b) =>
    {
      var aRect = a.GetClientRect();
      var bRect = b.GetClientRect();
      if (aRect.X == bRect.X)
      {
        return aRect.Y.CompareTo(bRect.Y);
      }
      return aRect.X.CompareTo(bRect.X);
    });
    return unidentifiedUniques;
  }
}
