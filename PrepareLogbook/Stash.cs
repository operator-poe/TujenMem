using System.Collections;
using ItemFilterLibrary;
using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace TujenMem.PrepareLogbook;


public class Stash
{
  public string[] _currencyNames = {
    "Scroll of Wisdom",
    "Orb of Scouring",
    "Orb of Alchemy",
    "Orb of Binding",
    "Chaos Orb",
    "Blessed Orb",
    "Divine Orb",
    "Cartographer's Chisel"
  };

  public Dictionary<string, Currency> Currencies { get; set; } = new Dictionary<string, Currency>();

  public Currency Wisdom
  {
    get
    {
      return Currencies["Scroll of Wisdom"];
    }
  }

  public Currency Scouring
  {
    get
    {
      return Currencies["Orb of Scouring"];
    }
  }

  public Currency Alchemy
  {
    get
    {
      return Currencies["Orb of Alchemy"];
    }
  }

  public Currency Binding
  {
    get
    {
      return Currencies["Orb of Binding"];
    }
  }

  public Currency Chaos
  {
    get
    {
      return Currencies["Chaos Orb"];
    }
  }

  public Currency Blessed
  {
    get
    {
      return Currencies["Blessed Orb"];
    }
  }

  public Currency Divine
  {
    get
    {
      return Currencies["Divine Orb"];
    }
  }

  public Currency Regal
  {
    get
    {
      return Currencies["Regal Orb"];
    }
  }

  public Currency Chisel
  {
    get
    {
      return Currencies["Cartographer's Chisel"];
    }
  }

  public Stash(bool refresh = true)
  {
    if (refresh)
    {
      RefreshCurrencies();
    }
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
    // order by x and y
    cnt = unidentifiedUniques.Count;
    var x = 0;
    unidentifiedUniques.Sort((a, b) =>
    {
      Log.Debug("Parsing item " + x + "/" + cnt);

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