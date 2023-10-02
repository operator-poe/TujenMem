using ExileCore;

namespace TujenMem;

public class HaggleCurrency
{
  private TujenMemSettings Settings;
  public string Name { get; set; }
  public int Value { get; set; }

  public bool Disabled => false;

  public override string ToString()
  {
    return $"{Name}: {Value}";
  }

  public HaggleCurrency(string name, int value, TujenMemSettings settings)
  {
    Name = name;
    Value = value;
    Settings = settings;
  }

  public float TotalValue()
  {
    var multiplier = 1f;
    if (Name.Contains("Lesser"))
    {
      multiplier = Settings.ArtifactValueSettings.ValueLesser;
    }
    else if (Name.Contains("Greater"))
    {
      multiplier = Settings.ArtifactValueSettings.ValueGreater;
    }
    else if (Name.Contains("Grand"))
    {
      multiplier = Settings.ArtifactValueSettings.ValueGrand;
    }
    else if (Name.Contains("Exceptional"))
    {
      multiplier = Settings.ArtifactValueSettings.ValueExceptional;
    }

    return Value * multiplier;
  }
}