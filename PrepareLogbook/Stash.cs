using System.Collections;
using System.Collections.Generic;
using ExileCore;
using ExileCore.Shared;

namespace TujenMem.PrepareLogbook;


public class Stash
{
  public string[] _currencyNames = {
    "Scroll of Wisdom",
    "Orb of Scouring",
    "Orb of Alchemy",
    "Chaos Orb",
    "Blessed Orb",
    "Divine Orb"
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

  public Stash()
  {
    RefreshCurrencies();
  }

  public IEnumerator CleanUp()
  {
    foreach (var currency in Currencies.Values)
    {
      if (currency.Holding)
      {
        yield return currency.Release();
      }
    }
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
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


}