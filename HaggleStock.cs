

namespace TujenMem;

public class HaggleStock
{
  public HaggleCurrency Lesser { get; set; }
  public HaggleCurrency Greater { get; set; }
  public HaggleCurrency Grand { get; set; }
  public HaggleCurrency Exceptional { get; set; }
  public int Coins { get; set; }

  public HaggleStock()
  {
    Lesser = new HaggleCurrency("Lesser", 0);
    Greater = new HaggleCurrency("Greater", 0);
    Grand = new HaggleCurrency("Grand", 0);
    Exceptional = new HaggleCurrency("Exceptional", 0);
  }
}