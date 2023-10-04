using System;
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

    Log.Debug($"HaggleProcess initialized with {Stock.Coins} coins.");
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

  public bool Update()
  {
    return UpdateStock();
  }

  public bool CanRun()
  {
    var canRun = Stock.Coins > 0
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableLesser || Stock.Lesser.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGreater || Stock.Greater.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGrand || Stock.Grand.Value > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableExceptional || Stock.Exceptional.Value > 300);
    ;

    Log.Debug($"CanRun: {canRun} - Coins: {Stock.Coins} - Lesser: {Stock.Lesser.Value} - Greater: {Stock.Greater.Value} - Grand: {Stock.Grand.Value} - Exceptional: {Stock.Exceptional.Value}");

    return canRun;
  }

  private bool UpdateStock()
  {
    Log.Debug("Update of Stock requested.");
    if (Stock == null)
    {
      Stock = new HaggleStock(Settings);
    }

    Log.Debug($"Stock: {Stock.Coins} - Lesser: {Stock.Lesser.Value} - Greater: {Stock.Greater.Value} - Grand: {Stock.Grand.Value} - Exceptional: {Stock.Exceptional.Value}");

    var currency = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo;
    var reRolls = currency.TujenRerolls;
    try
    {
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
    catch (Exception e)
    {
      Log.Error($"Error while updating stock: {e.ToString()}");
      try
      {
        List<int> values = new List<int>();
        foreach (var child in currency.Children)
        {
          if (!child.IsVisible)
          {
            continue;
          }
          foreach (var child2 in child.Children)
          {
            if (child2.Text == null)
            {
              continue;
            }
            if (child2.Text.Contains("."))
            {
              var value = int.Parse(child2.Text.Replace(".", ""));
              values.Add(value);
            }
          }
        }
        if (values.Count != 4)
        {
          Log.Error($"Error while updating stock: Could not find all values. Found {values.Count} values.");
          Log.Error(values.ToString());
          return false;
        }
        var lesser = values[0];
        var greater = values[1];
        var grand = values[2];
        var exceptional = values[3];
        Stock.Lesser.Value = lesser;
        Stock.Greater.Value = greater;
        Stock.Grand.Value = grand;
        Stock.Exceptional.Value = exceptional;
        Stock.Coins = reRolls;
      }
      catch (Exception e2)
      {
        Log.Error($"Error while updating stock: {e2.ToString()}");
        return false;
      }
    }

    Log.Debug($"Stock New: {Stock.Coins} - Lesser: {Stock.Lesser.Value} - Greater: {Stock.Greater.Value} - Grand: {Stock.Grand.Value} - Exceptional: {Stock.Exceptional.Value}");
    return true;
  }
}