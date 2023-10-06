using System;
using System.Collections;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements.ExpeditionElements;
using ExileCore.Shared;

namespace TujenMem;

public class HaggleProcess
{
  public HaggleStock Stock;

  public HaggleProcess(ExpeditionVendorElement haggleWindow, GameController gameController, TujenMemSettings settings)
  {
    Log.Debug($"HaggleProcess initialized with {HaggleStock.Coins} coins.");
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


  public bool CanRun()
  {
    var canRun = HaggleStock.Coins > 0
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableLesser || HaggleStock.Lesser > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGreater || HaggleStock.Greater > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableGrand || HaggleStock.Grand > 300)
            && (!TujenMem.Instance.Settings.ArtifactValueSettings.EnableExceptional || HaggleStock.Exceptional > 300);
    ;

    Log.Debug($"CanRun: {canRun} - Coins: {HaggleStock.Coins} - Lesser: {HaggleStock.Lesser} - Greater: {HaggleStock.Greater} - Grand: {HaggleStock.Grand} - Exceptional: {HaggleStock.Exceptional}");

    return canRun;
  }
}