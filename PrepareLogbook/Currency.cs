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

  public async SyncTask<bool> Hold()
  {
    await InputAsync.MoveMouseToElement(Position);
    await InputAsync.Wait();
    await InputAsync.KeyDown(Keys.ShiftKey);
    await InputAsync.Click(MouseButtons.Right);
    await InputAsync.Wait();
    Holding = true;
    return true;
  }

  public async SyncTask<bool> Release()
  {
    await InputAsync.Wait();
    await InputAsync.KeyUp(Keys.ShiftKey);
    await InputAsync.Wait();
    Holding = false;
    return true;
  }

  public async SyncTask<bool> Use(Vector2 position)
  {
    if (!Holding)
    {
      await Hold();
    }
    await InputAsync.MoveMouseToElement(position);
    await InputAsync.Wait();
    await InputAsync.Click(MouseButtons.Left);
    await InputAsync.Wait();
    return true;
  }

  public async SyncTask<bool> Hover()
  {
    await InputAsync.MoveMouseToElement(Position);
    await InputAsync.Wait();
    return true;
  }

  public async SyncTask<bool> GetStack()
  {
    await Hover();
    await InputAsync.WaitX(5);
    await InputAsync.KeyDown(Keys.ControlKey);
    await InputAsync.Click(MouseButtons.Left);
    await InputAsync.KeyUp(Keys.ControlKey);
    await InputAsync.Wait();
    return true;
  }

  public async SyncTask<bool> GetFraction(int num)
  {
    await Hover();
    await InputAsync.KeyDown(Keys.ShiftKey);
    await InputAsync.Click(MouseButtons.Left);
    await InputAsync.KeyUp(Keys.ShiftKey);
    await InputAsync.Wait(() => TujenMem.Instance.GameController.IngameState.IngameUi.GetChildAtIndex(149) is { IsVisible: true }, 1000, "Split window not opened");
    var numAsString = num.ToString();

    // iterate each number and send the key
    foreach (var c in numAsString)
    {
      var key = (Keys)c;
      Input.KeyDown(key);
      Input.KeyUp(key);
      await InputAsync.Wait();
    }
    await InputAsync.Wait();
    await InputAsync.KeyDown(Keys.Enter);
    await InputAsync.KeyUp(Keys.Enter);
    await InputAsync.Wait();
    return true;
  }
}