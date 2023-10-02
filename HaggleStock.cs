

namespace TujenMem;

public class HaggleStock
{
  private TujenMemSettings Settings;
  public HaggleCurrency Lesser { get; set; }
  public HaggleCurrency Greater { get; set; }
  public HaggleCurrency Grand { get; set; }
  public HaggleCurrency Exceptional { get; set; }
  public int Coins { get; set; }

  public HaggleStock(TujenMemSettings settings)
  {
    Settings = settings;
    Lesser = new HaggleCurrency("Lesser", 0, Settings);
    Greater = new HaggleCurrency("Greater", 0, Settings);
    Grand = new HaggleCurrency("Grand", 0, Settings);
    Exceptional = new HaggleCurrency("Exceptional", 0, Settings);
  }
}