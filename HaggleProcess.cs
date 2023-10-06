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
  public HaggleStock Stock;

  public HaggleProcess(ExpeditionVendorElement haggleWindow, GameController gameController, TujenMemSettings settings)
  {
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
    CurrentWindow = new HaggleProcessWindow();
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
    if (!TujenMem.Instance.Settings.DebugOnly)
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
      Stock = new HaggleStock();
    }

    Log.Debug($"Stock: {Stock.Coins} - Lesser: {Stock.Lesser.Value} - Greater: {Stock.Greater.Value} - Grand: {Stock.Grand.Value} - Exceptional: {Stock.Exceptional.Value}");

    var currency = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo;

    string reRollsString = currency.Children[1].Children[1].Text;
    string cleaned = new string(reRollsString.Where(char.IsDigit).ToArray()).Trim();
    var reRolls = int.Parse(cleaned);

    string lesserString = currency.Children[5].Children[1].Text;
    cleaned = new string(lesserString.Where(char.IsDigit).ToArray()).Trim();
    var lesser = int.Parse(cleaned);

    string greaterString = currency.Children[9].Children[1].Text;
    cleaned = new string(greaterString.Where(char.IsDigit).ToArray());
    var greater = int.Parse(cleaned);

    string grandString = currency.Children[13].Children[1].Text;
    cleaned = new string(grandString.Where(char.IsDigit).ToArray());
    var grand = int.Parse(cleaned);


    string exceptionalString = currency.Children[17].Children[1].Text;
    cleaned = new string(exceptionalString.Where(char.IsDigit).ToArray());
    var exceptional = int.Parse(cleaned);

    Stock.Lesser.Value = lesser;
    Stock.Greater.Value = greater;
    Stock.Grand.Value = grand;
    Stock.Exceptional.Value = exceptional;
    Stock.Coins = reRolls;

    Log.Debug($"Stock New: {Stock.Coins} - Lesser: {Stock.Lesser.Value} - Greater: {Stock.Greater.Value} - Grand: {Stock.Grand.Value} - Exceptional: {Stock.Exceptional.Value}");
    return true;
  }
}