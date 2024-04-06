

using System;
using System.Linq;

namespace TujenMem;

public enum StockType
{
  Tujen,
  Gwennen
}

public class HaggleStock
{
  public static StockType StockType = StockType.Tujen;

  private static int getStockTypeOffset(int offset)
  {
    switch (StockType)
    {
      case StockType.Tujen:
        return 1 + offset;
      case StockType.Gwennen:
        return 0 + offset;
      default:
        return 1 + offset;
    }
  }
  public static int Lesser
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(getStockTypeOffset(4), 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Lesser: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(getStockTypeOffset(4))));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Greater
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(getStockTypeOffset(8), 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Greater: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(getStockTypeOffset(8))));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Grand
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(getStockTypeOffset(12), 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Grand: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(getStockTypeOffset(12))));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Exceptional
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(getStockTypeOffset(16), 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Exceptional: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(getStockTypeOffset(16))));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Coins
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(getStockTypeOffset(0), 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Coins: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(getStockTypeOffset(0))));
        Error.Show();
      }
      return 0;
    }
  }
}