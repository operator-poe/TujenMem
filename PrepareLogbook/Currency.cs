using System.Collections;
using ExileCore;
using ExileCore.Shared;
using SharpDX;
using System.Windows.Forms;

namespace TujenMem.PrepareLogbook;

public class Currency
{
  public int ChildIndex = -1;
  public string Name;
  public bool Holding = false;
  public int StackSize
  {
    get
    {
      if (ChildIndex == -1)
      {
        return 0;
      }
      var stash = TujenMem.Instance.GameController.IngameState.IngameUi.StashElement;
      var item = stash.VisibleStash.VisibleInventoryItems[ChildIndex];
      var stackSize = item.Item.GetComponent<ExileCore.PoEMemory.Components.Stack>()?.Size ?? 0;
      return stackSize;
    }
  }

  public Vector2 Position
  {
    get
    {
      if (ChildIndex == -1)
      {
        return Vector2.Zero;
      }
      var stash = TujenMem.Instance.GameController.IngameState.IngameUi.StashElement;
      var item = stash.VisibleStash.VisibleInventoryItems[ChildIndex];
      var position = item.GetClientRect().Center;
      return position;
    }
  }

  public IEnumerator Hold()
  {
    Input.SetCursorPos(Position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Input.KeyDown(Keys.ShiftKey);
    Input.Click(MouseButtons.Right);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Holding = true;
  }

  public IEnumerator Release()
  {
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Input.KeyUp(Keys.ShiftKey);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Holding = false;
  }

  public IEnumerator Use(Vector2 position)
  {
    if (!Holding)
    {
      yield return Hold();
    }
    Input.SetCursorPos(position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Input.Click(MouseButtons.Left);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
  }

  public IEnumerator Hover()
  {
    Input.SetCursorPos(Position);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
  }

  public IEnumerator GetStack()
  {
    yield return Hover();
    yield return new WaitTime(100);
    Input.KeyDown(Keys.ControlKey);
    Input.Click(MouseButtons.Left);
    Input.KeyUp(Keys.ControlKey);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
  }

  public IEnumerator GetFraction(int num)
  {
    yield return Hover();
    Input.KeyDown(Keys.ShiftKey);
    Input.Click(MouseButtons.Left);
    Input.KeyUp(Keys.ShiftKey);
    yield return new WaitFunctionTimed(() => TujenMem.Instance.GameController.IngameState.IngameUi.GetChildAtIndex(149) is { IsVisible: true }, true, 1000, "Split window not opened");
    var numAsString = num.ToString();

    // iterate each number and send the key
    foreach (var c in numAsString)
    {
      var key = (Keys)c;
      Input.KeyDown(key);
      Input.KeyUp(key);
      yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    }
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
    Input.KeyDown(Keys.Enter);
    Input.KeyUp(Keys.Enter);
    yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
  }
}