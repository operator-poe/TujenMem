namespace TujenMem;

public class HaggleCurrency
{
  public string Name { get; set; }
  public int Value { get; set; }

  public bool Disabled => false;

  public override string ToString()
  {
    return $"{Name}: {Value}";
  }

  public HaggleCurrency(string name, int value)
  {
    Name = name;
    Value = value;
  }

  public float TotalValue()
  {
    var multiplier = 1f;
    if (Name.Contains("Lesser"))
    {
      multiplier = TujenMem.Instance.Settings.ArtifactValueSettings.ValueLesser;
    }
    else if (Name.Contains("Greater"))
    {
      multiplier = TujenMem.Instance.Settings.ArtifactValueSettings.ValueGreater;
    }
    else if (Name.Contains("Grand"))
    {
      multiplier = TujenMem.Instance.Settings.ArtifactValueSettings.ValueGrand;
    }
    else if (Name.Contains("Exceptional"))
    {
      multiplier = TujenMem.Instance.Settings.ArtifactValueSettings.ValueExceptional;
    }

    return Value * multiplier;
  }
}