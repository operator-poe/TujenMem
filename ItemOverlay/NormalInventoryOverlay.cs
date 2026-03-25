using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ImGuiNET;
using TujenMem.PrepareLogbook;

namespace TujenMem.ItemOverlay;

public class NormalInventoryOverlay : IInventoryOverlayRenderer
{
    private readonly CachedValue<List<OverlayDrawItem>> _cache;

    public NormalInventoryOverlay()
    {
        _cache = new TimeCache<List<OverlayDrawItem>>(BuildCache, 250);
    }

    public bool ShouldRender()
    {
        return TujenMem.Instance.Settings.PrepareLogbookSettings.ShowItemOverlay &&
               TujenMem.Instance.GameController.IngameState.IngameUi
                   .InventoryPanel[InventoryIndex.PlayerInventory] is { IsVisible: true };
    }

    private List<OverlayDrawItem> BuildCache()
    {
        var items = new List<OverlayDrawItem>();
        var inventoryPanel = TujenMem.Instance.GameController.IngameState.IngameUi
            .InventoryPanel[InventoryIndex.PlayerInventory];
        if (inventoryPanel == null) return items;

        var prepSettings = TujenMem.Instance.Settings.PrepareLogbookSettings;
        var badMods = prepSettings.ModsBlackList.Value
            .Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => x.Length > 0)
            .ToList();

        var factionOrder = prepSettings.FactionOrder;
        var areaOrder = prepSettings.AreaOrder;

        foreach (var invItem in inventoryPanel.VisibleInventoryItems)
        {
            var entity = invItem.Item;
            var path = entity?.Path;
            if (path == null) continue;

            bool isLogbook = path.Contains("ExpeditionLogbook");
            bool isMap = path.Contains("/Maps/");
            if (!isLogbook && !isMap) continue;

            var readiness = EvaluateReadiness(entity, isLogbook, badMods, prepSettings, factionOrder, areaOrder);

            uint fillColor, borderColor;
            switch (readiness)
            {
                case ItemReadiness.Ready:
                    fillColor = 0x4000FF00;
                    borderColor = 0xFF00FF00;
                    break;
                case ItemReadiness.ModsOkQuantityLow:
                    fillColor = 0x4000FFFF;
                    borderColor = 0xFF00FFFF;
                    break;
                default:
                    fillColor = 0x400000FF;
                    borderColor = 0xFF0000FF;
                    break;
            }

            var rect = invItem.GetClientRect();
            items.Add(new OverlayDrawItem
            {
                TopLeft = new System.Numerics.Vector2(rect.X, rect.Y),
                BottomRight = new System.Numerics.Vector2(rect.X + rect.Width, rect.Y + rect.Height),
                FillColor = fillColor,
                BorderColor = borderColor,
            });
        }

        return items;
    }

    private static ItemReadiness EvaluateReadiness(
        ExileCore.PoEMemory.MemoryObjects.Entity entity,
        bool isLogbook,
        List<string> badMods,
        PrepareLogbookSettings settings,
        List<string> factionOrder,
        List<string> areaOrder)
    {
        var modsComponent = entity.GetComponent<ExileCore.PoEMemory.Components.Mods>();
        if (modsComponent == null) return ItemReadiness.NotReady;

        if (!modsComponent.Identified)
            return ItemReadiness.NotReady;

        var humanStats = modsComponent.HumanStats?.Select(x => x.ToLower()).ToList();
        if (humanStats != null && humanStats.Any(entry => badMods.Any(term => entry.Contains(term))))
            return ItemReadiness.NotReady;

        if (isLogbook && modsComponent.ItemRarity == ItemRarity.Rare)
        {
            if (!IsLogbookBlessed(entity, settings, factionOrder, areaOrder))
                return ItemReadiness.NotReady;
        }

        int quantity = 0;
        if (humanStats != null)
        {
            foreach (var stat in humanStats)
            {
                var match = Regex.Match(stat, @"\+(\d+)%.*quantity", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    quantity = int.Parse(match.Groups[1].Value);
                    break;
                }
            }
        }

        if (quantity < settings.MinQuantity)
            return ItemReadiness.ModsOkQuantityLow;

        return ItemReadiness.Ready;
    }

    private static bool IsLogbookBlessed(
        ExileCore.PoEMemory.MemoryObjects.Entity entity,
        PrepareLogbookSettings settings,
        List<string> factionOrder,
        List<string> areaOrder)
    {
        var expeditionSaga = entity.GetComponent<ExileCore.PoEMemory.Components.ExpeditionSaga>();
        if (expeditionSaga == null) return false;

        var areas = expeditionSaga.Areas;
        if (areas == null || areas.Count == 0) return false;

        areas.Sort((n1, n2) =>
        {
            int f1 = factionOrder.IndexOf(n1.Faction);
            int f2 = factionOrder.IndexOf(n2.Faction);
            f1 = f1 == -1 ? int.MaxValue : f1;
            f2 = f2 == -1 ? int.MaxValue : f2;
            int cmp = f1.CompareTo(f2);
            if (cmp != 0) return cmp;

            int a1 = areaOrder.IndexOf(n1.Name);
            int a2 = areaOrder.IndexOf(n2.Name);
            return a1.CompareTo(a2);
        });

        var area = areas[0];
        foreach (var mod in area.ImplicitMods)
        {
            var blessCheck = mod.Group switch
            {
                "MapExpeditionMaximumPlacementDistance" => (settings.Bless_ExplosiveRange.Value, settings.Bless_ExplosiveRange_Min.Value),
                "MapExpeditionExplosives" => (settings.Bless_NumberOfExplosives.Value, settings.Bless_NumberOfExplosives_Min.Value),
                "MapExpeditionExplosionRadius" => (settings.Bless_ExplosiveRadius.Value, settings.Bless_ExplosiveRadius_Min.Value),
                "MapExpeditionChestCount" => (settings.Bless_AdditionalChests.Value, settings.Bless_AdditionalChests_Min.Value),
                "MapExpeditionArtifactQuantity" => (settings.Bless_QuantityOfArtifactsByMonsters.Value, settings.Bless_QuantityOfArtifactsByMonsters_Min.Value),
                "MapExpeditionEliteMonsterQuantity" => (settings.Bless_NumberOfMonsterMarkers.Value, settings.Bless_NumberOfMonsterMarkers_Min.Value),
                "MapExpeditionRelics" => (settings.Bless_NumberOfRemnants.Value, settings.Bless_NumberOfRemnants_Min.Value),
                "MapExpeditionExtraRelicSuffixChance" => (settings.Bless_AdditionalSuffixMod.Value, settings.Bless_AdditionalSuffixMod_Min.Value),
                _ => ((bool?)null, 0),
            };

            if (blessCheck.Item1 == null || !blessCheck.Item1.Value) continue;
            if (mod.Values[0] < blessCheck.Item2) return false;
        }

        return true;
    }

    public void Render(ImDrawListPtr drawList)
    {
        foreach (var item in _cache.Value)
        {
            if (item.FillColor != 0)
                drawList.AddRectFilled(item.TopLeft, item.BottomRight, item.FillColor);
            if (item.BorderColor != 0)
                drawList.AddRect(item.TopLeft, item.BottomRight, item.BorderColor, 0, ImDrawFlags.None, 2);
        }
    }
}
