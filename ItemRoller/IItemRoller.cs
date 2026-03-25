using System.Collections.Generic;
using ExileCore.PoEMemory.Elements.InventoryElements;

namespace TujenMem.ItemRoller;

public interface IItemRoller
{
    string Name { get; }
    bool IsEnabled { get; }

    bool CanHandle(string itemPath);
    ReRollable CreateReRollable(NormalInventoryItem item);

    IEnumerable<string> RequiredCurrencies { get; }
    IReadOnlyList<CurrencyRound> GetCurrencyRounds();

    ItemReadiness GetReadiness(ReRollable item);
}
