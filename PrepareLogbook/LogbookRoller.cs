using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Enums;
using TujenMem.ItemRoller;

namespace TujenMem.PrepareLogbook;

public class LogbookRoller : IItemRoller
{
    private readonly PrepareLogbookSettings _settings;

    public LogbookRoller(PrepareLogbookSettings settings)
    {
        _settings = settings;
    }

    public string Name => "Logbook";

    public bool IsEnabled => _settings.EnableRolling || _settings.EnableBlessing;

    public bool CanHandle(string itemPath) => itemPath.Contains("ExpeditionLogbook");

    public ReRollable CreateReRollable(NormalInventoryItem item)
    {
        return new Logbook { GridPosition = item.GetClientRect() };
    }

    public IEnumerable<string> RequiredCurrencies
    {
        get
        {
            yield return "Scroll of Wisdom";
            yield return "Orb of Scouring";
            yield return "Orb of Alchemy";
            yield return "Orb of Binding";
            yield return "Chaos Orb";
            yield return "Regal Orb";
            if (_settings.EnableBlessing)
                yield return "Blessed Orb";
        }
    }

    public IReadOnlyList<CurrencyRound> GetCurrencyRounds()
    {
        var rounds = new List<CurrencyRound>();
        var badMods = _settings.ModsBlackList.Value
            .Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => x.Length > 0)
            .ToList();

        if (_settings.EnableRolling)
        {
            rounds.Add(new CurrencyRound
            {
                Name = "Identify",
                CurrencyName = "Scroll of Wisdom",
                Applies = item => !item.IsIdentified,
                BatchMode = true,
            });

            rounds.Add(new CurrencyRound
            {
                Name = "Scour Magic",
                CurrencyName = "Orb of Scouring",
                Applies = item => item.Rarity == ItemRarity.Magic,
                BatchMode = true,
            });

            if (_settings.UseChaos)
            {
                rounds.Add(new CurrencyRound
                {
                    Name = "Alch Normal",
                    CurrencyName = "Orb of Alchemy",
                    Applies = item => item.Rarity == ItemRarity.Normal,
                    BatchMode = true,
                });

                rounds.Add(new CurrencyRound
                {
                    Name = "Chaos Spam",
                    CurrencyName = "Chaos Orb",
                    Applies = item => true,
                    IsComplete = item => !ShouldContinueProcessing(item, badMods),
                    Iterative = true,
                    BatchMode = false,
                    MaxIterations = 50,
                });
            }
            else
            {
                rounds.Add(new CurrencyRound
                {
                    Name = "Alch/Scour Batch",
                    CurrencyName = "Orb of Alchemy",
                    AlternativeCurrency = "Orb of Binding",
                    Applies = item => true,
                    IsComplete = item => !ShouldContinueProcessing(item, badMods),
                    Iterative = true,
                    BatchMode = true,
                    MaxIterations = 50,
                });
            }
        }

        if (_settings.EnableBlessing)
        {
            rounds.Add(new CurrencyRound
            {
                Name = "Bless Implicits",
                CurrencyName = "Blessed Orb",
                Applies = item => item.Rarity == ItemRarity.Rare && item is Logbook,
                IsComplete = item => item is Logbook lb && lb.IsBlessed,
                Iterative = true,
                BatchMode = false,
                MaxIterations = 100,
            });
        }

        return rounds;
    }

    public ItemReadiness GetReadiness(ReRollable item)
    {
        if (!item.IsIdentified)
            return ItemReadiness.NotReady;

        var badMods = _settings.ModsBlackList.Value
            .Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => x.Length > 0)
            .ToList();

        if (item.Mods.Any(entry => badMods.Any(term => entry.Contains(term))))
            return ItemReadiness.NotReady;

        if (item is Logbook logbook && logbook.Rarity == ItemRarity.Rare && !logbook.IsBlessed)
            return ItemReadiness.NotReady;

        var quantity = item.Quantity ?? item.QuantityFromStats ?? 0;
        if (quantity < _settings.MinQuantity)
            return ItemReadiness.ModsOkQuantityLow;

        return ItemReadiness.Ready;
    }

    private bool ShouldContinueProcessing(ReRollable item, List<string> badMods)
    {
        bool isQuantityTooLow = (item.Quantity ?? 0) < _settings.MinQuantity;
        bool hasBadMods = item.Mods.Any(entry => badMods.Any(term => entry.Contains(term)));
        return isQuantityTooLow || hasBadMods;
    }
}
