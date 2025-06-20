using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using TujenMem;

namespace TujenMem.Stash;
public class StashTab
{
    private int _index;

    public StashTab(int index)
    {
        _index = index;
    }

    public StashTab(string name)
    {
        _index = Ex.StashElement.Inventories.Select(i => i.TabName).ToList().IndexOf(name);
    }

    public string Name
    {
        get
        {
            return Ex.StashElement.Inventories[_index].TabName;
        }
    }

    public bool IsSelected
    {
        get
        {
            return Ex.StashElement.IndexInParent == _index;
        }
    }

    public int Capacity
    {
        get
        {
            return (int)(Ex.StashElement.Inventories[_index].Inventory.TotalBoxesInInventoryRow * Ex.StashElement.Inventories[_index].Inventory.TotalBoxesInInventoryRow);
        }
    }

    public StashTabContainerInventory StashElement
    {
        get
        {
            return Ex.StashElement.Inventories[_index];
        }
    }
}