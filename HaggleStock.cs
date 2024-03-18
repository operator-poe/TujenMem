

using System;
using System.Linq;

namespace TujenMem;

public class HaggleStock
{
  public static int Lesser
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(5, 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Lesser: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(5)));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Greater
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(9, 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Greater: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(9)));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Grand
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(13, 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Grand: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(13)));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Exceptional
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(17, 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Exceptional: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(17)));
        Error.Show();
      }
      return 0;
    }
  }

  public static int Coins
  {
    get
    {
      var text = TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildFromIndices(1, 1).Text;
      string cleaned = new string(text.Where(char.IsDigit).ToArray()).Trim();
      try
      {
        var var = int.Parse(cleaned);
        return var;
      }
      catch (Exception e)
      {
        Error.Add("Error while reading artifacts", $"Error parsing Coins: {e}\nText: {text}\nCleaned: {cleaned}");
        Error.Add("Relevant Structure", Error.VisualizeElementTree(TujenMem.Instance.GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.GetChildAtIndex(1)));
        Error.Show();
      }
      return 0;
    }
  }
}