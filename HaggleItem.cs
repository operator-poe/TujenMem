using SharpDX;

namespace TujenMem;

public enum HaggleItemState
{
  None,
  Rejected,
  Unpriced,
  Priced,
  TooExpensive,
  Bought
}

public class HaggleItem
{
  public long Address { get; set; }
  public Vector2 Position { get; set; }
  public HaggleCurrency Price { get; set; }

  public string Name { get; set; }
  public string Type { get; set; }
  public int Amount { get; set; }
  public float Value { get; set; }

  public HaggleItemState State { get; set; } = HaggleItemState.None;
}