using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Enums;
using TujenMem.ItemRoller;

namespace TujenMem.MapRoller;

public class MapRoller : IItemRoller
{
    private readonly MapRollerSettings _settings;
    private string _cachedBlacklistRaw;
    private List<string> _cachedBlacklist = new();

    public MapRoller(MapRollerSettings settings)
    {
        _settings = settings;
    }

    private List<string> GetParsedBlacklist()
    {
        var raw = _settings.ModsBlackList.Value;
        if (raw != _cachedBlacklistRaw)
        {
            _cachedBlacklistRaw = raw;
            _cachedBlacklist = raw
                .Split(',')
                .Select(x => x.Trim().ToLower())
                .Where(x => x.Length > 0)
                .ToList();
        }
        return _cachedBlacklist;
    }

    public string Name => "Map";

    public bool IsEnabled => _settings.EnableRolling;

    public bool CanHandle(string itemPath) => itemPath.Contains("/Maps/");

    public ReRollable CreateReRollable(NormalInventoryItem item)
    {
        return new Map
        {
            GridPosition = item.GetClientRect(),
            DirectSlotRef = item,
            Quality = item.Item.GetComponent<ExileCore.PoEMemory.Components.Quality>()?.ItemQuality ?? 0,
            Tier = item.Item.GetComponent<ExileCore.PoEMemory.Components.MapKey>()?.Tier ?? 0,
        };
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
            if (_settings.EnableExalting)
                yield return "Exalted Orb";
        }
    }

    public IReadOnlyList<CurrencyRound> GetCurrencyRounds()
    {
        var rounds = new List<CurrencyRound>();
        var badMods = GetParsedBlacklist();

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

        if (_settings.EnableExalting)
        {
            rounds.Add(new CurrencyRound
            {
                Name = "Exalt",
                CurrencyName = "Exalted Orb",
                Applies = item => item.Rarity == ItemRarity.Rare && HasOpenModSlots(item),
                IsComplete = item => !HasOpenModSlots(item),
                Iterative = true,
                BatchMode = false,
                MaxIterations = 6,
            });
        }

        return rounds;
    }

    public ItemReadiness GetReadiness(ReRollable item)
    {
        if (!item.IsIdentified)
            return ItemReadiness.NotReady;

        var badMods = GetParsedBlacklist();

        if (item.Mods.Any(entry => badMods.Any(term => entry.Contains(term))))
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

    private static bool HasOpenModSlots(ReRollable item)
    {
        var modsComponent = item.Item?.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        if (modsComponent == null) return false;

        var prefixCount = modsComponent.ItemMods?.Count(m => m.Group?.StartsWith("") ?? false) ?? 0;
        // Rare items have up to 6 explicit mods (3 prefix + 3 suffix)
        var totalMods = modsComponent.ItemMods?.Count ?? 0;
        return totalMods < 6;
    }
}
