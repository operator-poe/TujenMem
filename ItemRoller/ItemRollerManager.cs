using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared;
using ExileCore.Shared.Enums;

namespace TujenMem.ItemRoller;

public class ItemRollerManager
{
    private readonly List<IItemRoller> _rollers = new();

    public IReadOnlyList<IItemRoller> Rollers => _rollers;

    public void Register(IItemRoller roller) => _rollers.Add(roller);

    public IItemRoller FindRoller(string itemPath)
    {
        foreach (var roller in _rollers)
        {
            if (roller.CanHandle(itemPath))
                return roller;
        }
        return null;
    }

    public async SyncTask<bool> RollAll()
    {
        var allCurrencies = _rollers
            .Where(r => r.IsEnabled)
            .SelectMany(r => r.RequiredCurrencies)
            .Distinct()
            .ToList();

        if (allCurrencies.Count == 0)
        {
            Log.Debug("No rollers enabled, nothing to do");
            return true;
        }

        var stash = new RollerStash(allCurrencies);

        var inventory = TujenMem.Instance.GameController.IngameState.IngameUi
            .InventoryPanel[ExileCore.Shared.Enums.InventoryIndex.PlayerInventory];
        var inventoryItems = inventory.VisibleInventoryItems;

        foreach (var roller in _rollers)
        {
            if (!roller.IsEnabled) continue;

            var items = new List<ReRollable>();
            foreach (var invItem in inventoryItems)
            {
                var path = invItem.Item?.Path;
                if (path != null && roller.CanHandle(path))
                {
                    items.Add(roller.CreateReRollable(invItem));
                }
            }

            if (items.Count == 0)
            {
                Log.Debug($"[{roller.Name}] No applicable items found");
                continue;
            }

            Log.Debug($"[{roller.Name}] Found {items.Count} items to process");

            var rounds = roller.GetCurrencyRounds();
            foreach (var round in rounds)
            {
                await ExecuteRound(round, items, stash);
                await stash.CleanUp();
            }
        }

        return await stash.CleanUp();
    }

    private static async SyncTask<bool> ExecuteRound(CurrencyRound round, List<ReRollable> items, RollerStash stash)
    {
        Log.Debug($"Executing round: {round.Name} with {round.CurrencyName}");

        var applicable = items.Where(i => !i.IsCorrupted && round.Applies(i)).ToList();
        if (applicable.Count == 0)
        {
            Log.Debug($"Round {round.Name}: no applicable items");
            return true;
        }

        var currency = ResolveCurrency(round, stash);
        if (currency == null)
        {
            Log.Error($"Round {round.Name}: currency {round.CurrencyName} not found in stash");
            return false;
        }

        if (round.Iterative)
        {
            await ExecuteIterativeRound(round, applicable, currency, stash);
        }
        else if (round.BatchMode)
        {
            await ExecuteBatchRound(round, applicable, currency);
        }
        else
        {
            await ExecuteSinglePassRound(applicable, currency);
        }

        return true;
    }

    private static async SyncTask<bool> ExecuteBatchRound(
        CurrencyRound round, List<ReRollable> items, Currency currency)
    {
        foreach (var item in items)
        {
            if (round.IsComplete != null && round.IsComplete(item)) continue;
            await currency.Use(item.Position);
            await InputAsync.WaitX(2);
        }
        await currency.Release();
        await InputAsync.WaitX(5);
        return true;
    }

    private static async SyncTask<bool> ExecuteSinglePassRound(
        List<ReRollable> items, Currency currency)
    {
        foreach (var item in items)
        {
            await currency.Use(item.Position);
            await InputAsync.WaitX(5);
        }
        return true;
    }

    private static async SyncTask<bool> ExecuteIterativeRound(
        CurrencyRound round, List<ReRollable> items, Currency currency, RollerStash stash)
    {
        if (round.BatchMode)
        {
            int passCount = 0;
            bool needsMorePasses = true;

            while (needsMorePasses && passCount < round.MaxIterations)
            {
                passCount++;
                needsMorePasses = false;

                try
                {
                    foreach (var item in items)
                    {
                        if (item.IsCorrupted || item.IsHovered) continue;
                        await item.Hover();
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Round {round.Name}: error hovering: {e.Message}");
                }

                var toProcess = items.Where(i => !i.IsCorrupted && round.Applies(i) &&
                    (round.IsComplete == null || !round.IsComplete(i))).ToList();

                if (toProcess.Count == 0) break;

                // Scour pass (items that need to be reset to normal)
                var toScour = toProcess.Where(i => i.Rarity == ItemRarity.Magic || i.Rarity == ItemRarity.Rare).ToList();
                if (toScour.Count > 0)
                {
                    var scouring = stash.Get("Orb of Scouring");
                    if (scouring != null)
                    {
                        await scouring.Hold();
                        foreach (var item in toScour)
                        {
                            await scouring.Use(item.Position);
                            await InputAsync.WaitX(2);
                        }
                        await scouring.Release();
                        await InputAsync.WaitX(5);
                    }
                }

                // Alch pass (items that are now normal)
                var toAlch = toProcess.Where(i => i.Rarity == ItemRarity.Normal).ToList();
                if (toAlch.Count > 0)
                {
                    var alch = ResolveCurrency(round, stash);
                    await alch.Hold();
                    foreach (var item in toAlch)
                    {
                        await alch.Use(item.Position);
                        await InputAsync.WaitX(2);
                    }
                    await alch.Release();
                    await InputAsync.WaitX(5);
                }

                needsMorePasses = items.Any(i => !i.IsCorrupted && round.Applies(i) &&
                    (round.IsComplete == null || !round.IsComplete(i)));
            }

            if (passCount >= round.MaxIterations)
            {
                Log.Warning($"Round {round.Name}: stopped after {round.MaxIterations} passes");
            }
        }
        else
        {
            // Per-item chaos-style iterative rolling
            foreach (var item in items)
            {
                if (item.IsCorrupted) continue;
                await item.Hover();

                try
                {
                    if (round.IsComplete != null && round.IsComplete(item)) continue;
                }
                catch (Exception e)
                {
                    Log.Error($"Round {round.Name}: error checking completion: {e.Message}");
                    Log.Error($"{e.StackTrace}");
                }

                int iterations = 0;
                do
                {
                    if (item.Rarity == ItemRarity.Magic)
                    {
                        var regal = stash.Get("Regal Orb");
                        if (regal != null)
                        {
                            await regal.Use(item.Position);
                            await regal.Release();
                        }
                    }

                    await currency.Use(item.Position);
                    await InputAsync.WaitX(5);
                    iterations++;
                } while (iterations < round.MaxIterations &&
                         (round.IsComplete == null || !round.IsComplete(item)));
            }
        }

        return true;
    }

    private static Currency ResolveCurrency(CurrencyRound round, RollerStash stash)
    {
        if (!string.IsNullOrEmpty(round.AlternativeCurrency))
        {
            return stash.GetBestAlternative(round.CurrencyName, round.AlternativeCurrency);
        }
        return stash.Get(round.CurrencyName);
    }
}
