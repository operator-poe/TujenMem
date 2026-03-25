using ImGuiNET;

namespace TujenMem.ItemOverlay;

public interface IInventoryOverlayRenderer
{
    bool ShouldRender();
    void Render(ImDrawListPtr drawList);
}
