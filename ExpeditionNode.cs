using SharpDX;
using ExileCore.PoEMemory;

namespace TujenMem;

public class ExpeditionNode
{
  public string Area { get; set; }
  public string Faction { get; set; }

  public RectangleF Position { get; set; }

  public ExpeditionNode(string area, string faction, RectangleF position)
  {
    Area = area;
    Faction = faction;
    Position = position;
  }

  public static ExpeditionNode FromElement(Element node)
  {
    var nodeInfo = node.Tooltip.GetChildAtIndex(1);
    var areaName = nodeInfo.GetChildAtIndex(0).Text;
    var groupName = nodeInfo.GetChildAtIndex(1).Text;

    return new ExpeditionNode(areaName, groupName, node.GetClientRect());

  }
}