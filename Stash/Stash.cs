using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore.Shared;
using TujenMem;

namespace TujenMem.Stash;
public static class Stash
{
  public static bool IsVisible
  {
    get
    {
      return Ex.StashElement != null && Ex.StashElement.IsVisible;
    }
  }

  public static List<StashTab> Tabs
  {
    get
    {
      var tabs = new List<StashTab>();
      for (int i = 0; i < TujenMem.Instance.GameController.Game.IngameState.IngameUi.StashElement.TotalStashes; i++)
      {
        tabs.Add(new StashTab(i));
      }
      return tabs;
    }
  }

  public static StashTab ActiveTab
  {
    get
    {
      return Tabs[Ex.StashElement.IndexVisibleStash];
    }
  }

  public static List<string> TabNames
  {
    get
    {
      return Ex.StashElement.Inventories.Select(i => i.TabName).ToList();
    }
  }

  public static async SyncTask<bool> ScrollToTab(int index)
  {
    await InputAsync.MoveMouseToElement(Ex.TabSwitchBar.GetClientRect().Center);
    await InputAsync.Wait(10);
    await InputAsync.KeyDown(System.Windows.Forms.Keys.ControlKey);
    while (Ex.StashElement.IndexVisibleStash != index)
    {
      if (index < Ex.StashElement.IndexVisibleStash)
      {
        await InputAsync.VerticalScroll(true, 1);
      }
      else
      {
        await InputAsync.VerticalScroll(false, 1);
      }
      await InputAsync.Wait(1);
    }
    await InputAsync.Wait(() => Ex.StashElement.Inventories[index] != null, 500);
    await InputAsync.KeyUp(System.Windows.Forms.Keys.ControlKey);
    return true;
  }

  public static async SyncTask<bool> ClickTab(int index)
  {
    if (IsTabVisible(index))
    {
      await InputAsync.ClickElement(Ex.TabButtons[index].GetClientRect());
      await InputAsync.Wait(() => Ex.StashElement.Inventories[index] != null, 500);
    }
    else
    {
      return await ScrollToTab(index);
    }
    return true;
  }

  public static bool IsTabVisible(int index)
  {
    if (!Ex.StashPanel.IsVisible)
    {
      return false;
    }
    var tabNode = Ex.TabButtons[index];
    return Ex.StashPanel.GetClientRect().Intersects(tabNode.GetClientRect());
  }

  public static async SyncTask<bool> SelectTab(int index)
  {
    if (index < 0 || index >= Tabs.Count)
    {
      throw new ArgumentOutOfRangeException();
    }
    if (index == Ex.StashElement.IndexVisibleStash)
    {
      return true;
    }
    if (IsTabVisible(index))
    {
      return await InputAsync.ClickElement(Ex.TabButtons[index].GetClientRect());
    }
    else
    {
      return await ScrollToTab(index);
    }
  }
  public static async SyncTask<bool> SelectTab(string name)
  {
    return await SelectTab(Tabs.FindIndex(t => t.Name == name));
  }

  public static int TabIndex(string name)
  {
    return Tabs.FindIndex(t => t.Name == name);
  }

  public static async SyncTask<bool> EnsureStashesAreLoaded(List<string> stashNames)
  {
    await Util.ForceFocusAsync();
    await Utils.EnsureStash();
    foreach (var t in stashNames)
    {
      await SelectTab(t);
      int TabIndex = Stash.TabIndex(t);
      await InputAsync.Wait(() => Ex.StashElement.Inventories[TabIndex] != null, 500);
    }
    await InputAsync.Wait(20);
    return true;
  }

  public static bool AreStashesLoaded(List<string> stashNames)
  {
    foreach (var t in stashNames)
    {
      int TabIndex = Stash.TabIndex(t);
      if (Ex.StashElement.Inventories[TabIndex] == null)
      {
        return false;
      }
    }
    return true;
  }
}