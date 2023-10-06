using System;
using System.Globalization;
using System.IO;
using ExileCore;

namespace TujenMem;

public class Statistics
{
  private static string DataFolder
  {
    get
    {
      return Path.Combine(TujenMem.Instance.DirectoryFullName, "Statistics");
    }
  }

  private static string DataFileName
  {
    get
    {
      return Path.Combine(DataFolder, "Statistics.csv");
    }
  }

  public static string GetWindowId()
  {
    return Guid.NewGuid().ToString();
  }

  public static void RecordItem(string windowId, NinjaItem ninjaItem, HaggleItem haggleItem)
  {
    if (!Directory.Exists(DataFolder))
    {
      Directory.CreateDirectory(DataFolder);
    }
    if (!File.Exists(DataFileName))
    {
      using (StreamWriter sw = new StreamWriter(DataFileName, true))
      {
        sw.WriteLine("Timestamp;WindowId;ItemName;ItemType;NinjaChaosValue;ArtifactPrice;ArtifactType;Amount;ValueAtTheTime;State;GemLevel;GemQuality;MapTier;MapInfluenced;MapUnique");
      }
    }

    try
    {
      using (StreamWriter sw = new StreamWriter(DataFileName, true))
      {
        sw.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")};{windowId};{ninjaItem.Name};{haggleItem.Type};{ninjaItem.ChaosValue.ToString(CultureInfo.InvariantCulture)};{haggleItem.Price.Value.ToString(CultureInfo.InvariantCulture)};{haggleItem.Price.Name};{haggleItem.Amount};{haggleItem.Value.ToString(CultureInfo.InvariantCulture)};{haggleItem.State};{(haggleItem as HaggleItemGem)?.Level};{(haggleItem as HaggleItemGem)?.Quality};{(haggleItem as HaggleItemMap)?.MapTier};{(haggleItem as HaggleItemMap)?.IsInfluenced};{(haggleItem as HaggleItemMap)?.IsUnique}");
      }
    }
    catch (Exception e)
    {
      DebugWindow.LogError($"Error writing to file: {e.ToString()}");
    }
  }
}