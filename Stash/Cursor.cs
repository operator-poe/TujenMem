using System;
using System.Windows.Forms;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using TujenMem;

namespace TujenMem.Stash;

// If HasItem, but name is null it's a right click which we can't identify
public static class Cursor
{
    public static Element Element
    {
        get
        {
            return TujenMem.Instance.GameController.Game.IngameState.IngameUi.Cursor;
        }
    }

    public static bool HasItem
    {
        get
        {
            return Element.ChildCount > 0;
        }
    }

    public static Entity ItemEntity
    {
        get
        {
            if (!HasItem)
                return null;
            return Element.GetChildAtIndex(0).Entity;
        }
    }

    public static Base ItemBase
    {
        get
        {
            return ItemEntity?.GetComponent<Base>();
        }
    }

    public static Stack Stack
    {
        get
        {
            return ItemEntity?.GetComponent<Stack>();
        }
    }

    public static string ItemName
    {
        get
        {
            return ItemBase?.Name;
        }
    }

    public static int StackSize
    {
        get
        {
            return Stack?.Size ?? 0;
        }
    }

    public static async SyncTask<bool> ReleaseItemOnCursorAsync(Action callback = null)
    {

        await InputAsync.Wait(30);
        if (!HasItem)
        {
            callback?.Invoke();
            return true;
        }

        Log.Debug($"Dumping '{ItemName}' x{StackSize} on cursor");
        if (ItemName == null)
        {
            while (HasItem)
            {
                await InputAsync.Wait(30);
                await InputAsync.Click(MouseButtons.Right);
                await InputAsync.Wait(30);
            }
        }
        else
        {
            while (HasItem)
            {
                // if (ItemName == "Awakened Sextant" || ItemName == "Surveyor's Compass")
                // {
                //     // yield return NStash.Stash.SelectTab(AutoSextant.Instance.Settings.RestockSextantFrom.Value);
                //     if (ItemName == "Awakened Sextant")
                //     {
                //         var nextSextant = Stash.NextSextant;
                //         await InputAsync.ClickElement(nextSextant.Position, MouseButtons.Left);
                //         await InputAsync.Wait(30);
                //     }
                //     else if (ItemName == "Surveyor's Compass")
                //     {
                //         var nextSextant = Stash.NextCompass;
                //         await InputAsync.ClickElement(nextSextant.Position, MouseButtons.Left);
                //         await InputAsync.Wait(30);
                //     }

                // }
                // else if (ItemName == "Charged Compass")
                // {
                //     var nextFreeSlot = Inventory.NextFreeChargedCompassSlot;

                //     await InputAsync.ClickElement(nextFreeSlot.Position, MouseButtons.Left);
                //     await InputAsync.Wait(30);
                // }
            }
        }
        callback?.Invoke();
        return true;
    }
}
