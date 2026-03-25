using System.Collections.Generic;
using System.Diagnostics;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class ItemOverlayManager
{
    private const long UpdateIntervalMs = 250;

    private readonly List<IInventoryOverlayRenderer> _renderers = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public void Register(IInventoryOverlayRenderer renderer) => _renderers.Add(renderer);

    public void RenderAll(ImDrawListPtr drawList)
    {
        bool shouldUpdate = _stopwatch.ElapsedMilliseconds >= UpdateIntervalMs;
        if (shouldUpdate) _stopwatch.Restart();

        foreach (var renderer in _renderers)
        {
            if (renderer.ShouldRender())
            {
                if (shouldUpdate) renderer.Update();
                renderer.Render(drawList);
            }
        }
    }
}
