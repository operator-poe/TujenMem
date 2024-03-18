using System.Linq;

namespace TujenMem.BuyAssistance;

public class LogbookOffer
{
  public int Quantity { get; set; }
  public int Price { get; set; }
  public string Ign { get; set; }

  public int TotalPrice
  {
    get
    {
      return Quantity * Price;
    }
  }

  public int DivinePrice
  {
    get
    {
      Ninja.Items.TryGetValue("Divine Orb", out var divine);
      return (int)(divine?.First().ChaosValue ?? 230f);
    }
  }

  public int Divines
  {
    get
    {
      return TotalPrice / DivinePrice;
    }
  }

  public int Chaos
  {
    get
    {
      return TotalPrice % DivinePrice;
    }
  }
}