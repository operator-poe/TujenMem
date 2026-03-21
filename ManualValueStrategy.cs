using System.Collections.Generic;

namespace TujenMem;

public class ManualValueStrategy : IValueStrategy
{
    public void EvaluateItems(List<HaggleItem> items)
    {
        foreach (var item in items)
        {
            if (item.State != HaggleItemState.Unpriced)
                continue;

            var itemPrice = item.Price?.TotalValue() ?? 0;
            if (itemPrice * TujenMem.Instance.Settings.ArtifactValueSettings.ItemPriceMultiplier.Value >= item.Value)
            {
                item.State = HaggleItemState.TooExpensive;
            }
            else
            {
                item.State = HaggleItemState.Priced;
            }
        }
    }

    public void OnWindowComplete(List<HaggleItem> items)
    {
    }
}
