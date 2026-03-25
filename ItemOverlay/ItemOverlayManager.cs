using System.Collections.Generic;
using ImGuiNET;

namespace TujenMem.ItemOverlay;

public class ItemOverlayManager
{
    private readonly List<IInventoryOverlayRenderer> _renderers = new();

    public void Register(IInventoryOverlayRenderer renderer) => _renderers.Add(renderer);

    public void RenderAll(ImDrawListPtr drawList)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer.ShouldRender())
                renderer.Render(drawList);
        }
    }
}
