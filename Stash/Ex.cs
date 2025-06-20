using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using TujenMem;

namespace TujenMem.Stash;

public static class Ex
{
    public static GameController GameController
    {
        get
        {
            return TujenMem.Instance.GameController;
        }
    }

    public static StashElement StashElement
    {
        get
        {
            if (!GameController.IngameState.IngameUi.StashElement.IsVisible)
            {
                return null;
            }
            return GameController.IngameState.IngameUi.StashElement;
        }
    }

    public static StashTopTabSwitcher TabSwitchBar
    {
        get
        {
            return StashElement.StashTabContainer.TabSwitchBar;
        }
    }

    public static Element StashPanel
    {
        get
        {
            return StashElement.ViewAllStashPanel;
        }
    }

    public static IList<Element> TabButtons
    {
        get
        {
            return StashElement.TabListButtons;
        }
    }
}
