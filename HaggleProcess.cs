using System.Collections;
using System.Linq;
using ExileCore.Shared;

namespace TujenMem;

public class HaggleProcess
{
  public HaggleStock Stock;

  public HaggleProcess()
  {
    Log.Debug($"HaggleProcess initialized with {HaggleStock.Coins} coins.");
  }

  public HaggleProcessWindow CurrentWindow = null;

  public void InitializeWindow()
  {
    CurrentWindow = new HaggleProcessWindow();
  }

  public async SyncTask<bool> Run()
  {
    CurrentWindow.ReadItems();
    await TaskUtils.NextFrame();
    CurrentWindow.ApplyMappingToItems();
    await TaskUtils.NextFrame();
    CurrentWindow.FilterItems();
    await TaskUtils.NextFrame();

    var attempts = 0;
    while (CurrentWindow.Items.Any(x => x.State == HaggleItemState.Unpriced))
    {
      attempts++;
      if (attempts > 3)
      {
        Error.AddAndShow("Error pricing items.", $"Too many attempts to price items. Stopping.\nThis could be related to missing Ninja data.");
        return false;
      }
      await CurrentWindow.GetItemPrices();
      await TaskUtils.NextFrame();
    }
    await TaskUtils.NextFrame();
    if (!TujenMem.Instance.Settings.DebugOnly)
    {
      await CurrentWindow.HaggleForItems();
    }

    return true;
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