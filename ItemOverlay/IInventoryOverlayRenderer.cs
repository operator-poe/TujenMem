using ImGuiNET;

namespace TujenMem.ItemOverlay;

public struct OverlayDrawItem
{
    public System.Numerics.Vector2 TopLeft;
    public System.Numerics.Vector2 BottomRight;
    public uint FillColor;   // 0 = skip fill
    public uint BorderColor; // 0 = skip border
    public string Text;      // null = no text
    public uint TextColor;
}

public interface IInventoryOverlayRenderer
{
    bool ShouldRender();
    void Render(ImDrawListPtr drawList);
}
