using System.Collections.Generic;

namespace TujenMem;

public interface IValueStrategy
{
    void EvaluateItems(List<HaggleItem> items);
    void OnWindowComplete(List<HaggleItem> items);
}
