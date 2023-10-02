using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements.ExpeditionElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;

namespace TujenMem;

public class HaggleProcess
{
  private readonly GameController _gameController;
  private readonly ExpeditionVendorElement HaggleWindow;
  public HaggleStock Stock;

  private readonly Dictionary<string, List<NinjaItem>> _ninjaItems = new();

  private readonly TujenMemSettings Settings;

  public HaggleProcess(ExpeditionVendorElement haggleWindow, GameController gameController, Dictionary<string, List<NinjaItem>> ninjaItems, TujenMemSettings settings)
  {
    _gameController = gameController;
    HaggleWindow = haggleWindow;
    _ninjaItems = ninjaItems;
    Settings = settings;
    Initialize();

    DebugWindow.LogMsg($"HaggleProcess initialized with {Stock.Coins} coins.");
  }

  private void Initialize()
  {
    UpdateStock();
  }

  public HaggleProcessWindow CurrentWindow = null;

  public void InitializeWindow()
  {
    CurrentWindow = new HaggleProcessWindow(HaggleWindow, _gameController, Settings, _ninjaItems);
  }

  public IEnumerator Run()
  {
    CurrentWindow.ReadItems();
    yield return new WaitTime(0);
    CurrentWindow.ApplyMappingToItems();
    yield return new WaitTime(0);
    CurrentWindow.FilterItems();
    yield return new WaitTime(0);
    while (CurrentWindow.Items.Any(x => x.State == HaggleItemState.Unpriced))
    {
      yield return CurrentWindow.GetItemPrices();
      yield return new WaitTime(0);
    }
    yield return new WaitTime(0);
    if (!Settings.DebugOnly)
    {
      yield return CurrentWindow.HaggleForItems();
    }
  }

  public IEnumerator Update()
  {
    UpdateStock();
    yield break;
  }

  public bool CanRun()
  {
    return Stock.Coins > 0
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableLesser || Stock.Lesser.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGreater || Stock.Greater.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGrand || Stock.Grand.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableExceptional || Stock.Exceptional.Value > 300);
    ;
  }

  private void UpdateStock()
  {
    if (Stock == null)
    {
      Stock = new HaggleStock(Settings);
    }

    var currency = HaggleWindow.CurrencyInfo;

    var reRolls = currency.TujenRerolls;
    var lesser = int.Parse(currency.Children[5].Children[1].Text.Replace(".", ""));
    var greater = int.Parse(currency.Children[9].Children[1].Text.Replace(".", ""));
    var grand = int.Parse(currency.Children[13].Children[1].Text.Replace(".", ""));
    var exceptional = int.Parse(currency.Children[17].Children[1].Text.Replace(".", ""));

    Stock.Lesser.Value = lesser;
    Stock.Greater.Value = greater;
    Stock.Grand.Value = grand;
    Stock.Exceptional.Value = exceptional;
    Stock.Coins = reRolls;
  }
}