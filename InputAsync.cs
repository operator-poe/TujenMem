using System;
using System.Windows.Forms;
using ExileCore.Shared;
using SharpDX;
using InputHumanizer.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TujenMem;

public class InputAsync : ExileCore.Input
{
  private static TujenMem Instance = TujenMem.Instance;
  public static IInputController _inputController = null;
  public static bool LOCK_CONTROLLER = false;

  public static System.Numerics.Vector2 V2(SharpDX.Vector2 sharpDxVector)
  {
    // Random number generator
    Random random = new Random();

    // Generate random offsets within the range of -5 to 5 for both X and Y
    float randomOffsetX = (float)(random.NextDouble() * 10.0 - 5.0);
    float randomOffsetY = (float)(random.NextDouble() * 10.0 - 5.0);

    // Create the new Vector2 with added randomness
    return new System.Numerics.Vector2(sharpDxVector.X + randomOffsetX, sharpDxVector.Y + randomOffsetY);
  }

  private static void IController()
  {
    LOCK_CONTROLLER = true;
    if (_inputController != null)
      return;
    var tryGetInputController = Instance.GameController.PluginBridge.GetMethod<Func<string, IInputController>>("InputHumanizer.TryGetInputController");
    if (tryGetInputController == null)
    {
      Log.Error("InputHumanizer method not registered.");
      return;
    }

    _inputController = tryGetInputController("TujenMem");
    if (_inputController == null)
    {
      Log.Error("Failed to get InputHumanizer controller");
      throw new Exception("Failed to get InputHumanizer controller");
    }
  }

  public static void IControllerEnd()
  {
    if (LOCK_CONTROLLER)
      return;
    _inputController?.Dispose();
    _inputController = null;
  }

  public static async SyncTask<bool> ClickElement(Vector2 pos, MouseButtons mouseButton = MouseButtons.Left)
  {
    IController();
    await _inputController.MoveMouse(V2(pos));
    await _inputController.Click(mouseButton);
    IControllerEnd();
    return true;
  }


  public static async SyncTask<bool> ClickElement(Vector2 pos)
  {
    return await ClickElement(pos, MouseButtons.Left);
  }

  public static new async SyncTask<bool> Click(MouseButtons mouseButton = MouseButtons.Left)
  {
    IController();
    await _inputController.Click(mouseButton);
    IControllerEnd();
    return true;
  }

  public static async SyncTask<bool> MoveMouseToElement(Vector2 pos)
  {
    IController();
    await _inputController.MoveMouse(V2(pos + Instance.GameController.Window.GetWindowRectangle().TopLeft));
    IControllerEnd();
    return true;
  }

  public static async SyncTask<bool> Delay(int ms = 0)
  {
    await Wait(ms);
    return true;
  }

  public static new async SyncTask<bool> VerticalScroll(bool scrollUp, int clicks)
  {
    const int wheelDelta = 120;
    if (scrollUp)
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, clicks * wheelDelta, 0);
    else
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, -(clicks * wheelDelta), 0);
    return await InputAsync.Wait(1);
    // return await _inputController.VerticalScroll(scrollUp, clicks * wheelDelta);
  }

  private static Vector2 LastMousePosition = Vector2.Zero;
  public static void StorePosition()
  {
    var pos = ForceMousePositionNum;
    LastMousePosition = new Vector2(pos.X, pos.Y);
  }

  public static void RestorePosition()
  {
#pragma warning disable CS0612 // Type or member is obsolete
    SetCursorPos(LastMousePosition);
#pragma warning restore CS0612 // Type or member is obsolete
    LastMousePosition = Vector2.Zero;
  }

  public static async SyncTask<bool> HoldCtrl()
  {
    return await KeyDown(Keys.ControlKey);
  }

  public static async SyncTask<bool> ReleaseCtrl()
  {
    return await KeyUp(Keys.ControlKey);
  }

  public static async SyncTask<bool> HoldShift()
  {
    return await KeyDown(Keys.ShiftKey);
  }

  public static async SyncTask<bool> ReleaseShift()
  {
    return await KeyUp(Keys.ShiftKey);
  }

  public static async SyncTask<bool> WaitX(int times)
  {
    IController();
    for (int i = 0; i < times; i++)
      await Wait(_inputController.GenerateDelay());
    IControllerEnd();
    return true;
  }

  public static async SyncTask<bool> Wait()
  {
    IController();
    await Wait(_inputController.GenerateDelay());
    IControllerEnd();
    return true;
  }
  public static async SyncTask<bool> Wait(int ms)
  {
    return await Wait(TimeSpan.FromMilliseconds(ms));
  }

  public static async SyncTask<bool> Wait(TimeSpan period)
  {
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < period)
    {
      await TaskUtils.NextFrame();
    }

    return true;
  }

  public static async SyncTask<bool> Wait(Func<bool> fn, TimeSpan period, string ErrorMessage = "")
  {
    var sw = Stopwatch.StartNew();
    while (!fn() && sw.Elapsed < period)
      await TaskUtils.NextFrame();

    if (!fn())
    {
      if (ErrorMessage != "")
        Log.Error(ErrorMessage);
      return false;
    }
    return true;

  }
  public static async SyncTask<bool> Wait(Func<bool> fn, int ms = 100, string ErrorMessage = "")
  {
    return await Wait(fn, TimeSpan.FromMilliseconds(ms), ErrorMessage);
  }
  public static async SyncTask<bool> WaitWhile(Func<bool> condition, int ms = 100)
  {
    while (condition())
    {
      await Wait(ms);
    }

    return true;
  }

  public static new async SyncTask<bool> KeyDown(Keys key)
  {
    IController();
    await _inputController.KeyDown(key);
    IControllerEnd();
    return true;
  }

  public static new async SyncTask<bool> KeyUp(Keys key)
  {
    IController();
    await _inputController.KeyUp(key);
    IControllerEnd();
    return true;
  }

  public static new async SyncTask<bool> KeyPress(Keys key)
  {
    IController();
    await _inputController.KeyDown(key);
    await _inputController.KeyUp(key);
    IControllerEnd();
    return true;
  }

  public static void RegisterKey(Keys key, bool suppress = false)
  {
    if (suppress)
      KeySuppressionManager.SuppressKey(key);
    ExileCore.Input.RegisterKey(key);
  }
}

public static class KeySuppressionManager
{
  private const int WH_KEYBOARD_LL = 13;
  private const int WM_KEYDOWN = 0x0100;
  private static LowLevelKeyboardProc _proc = HookCallback;
  private static IntPtr _hookID = IntPtr.Zero;
  private static HashSet<Keys> _suppressedKeys = new HashSet<Keys>();

  static KeySuppressionManager()
  {
    _hookID = SetHook(_proc);
    Application.ApplicationExit += (sender, e) => UnhookWindowsHookEx(_hookID);
  }

  public static void SuppressKey(Keys key)
  {
    _suppressedKeys.Add(key);
  }

  public static void AllowKey(Keys key)
  {
    _suppressedKeys.Remove(key);
  }

  private static IntPtr SetHook(LowLevelKeyboardProc proc)
  {
    using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
    using (var curModule = curProcess.MainModule)
    {
      return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
    }
  }

  private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

  private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
  {
    if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
    {
      int vkCode = Marshal.ReadInt32(lParam);
      Keys key = (Keys)vkCode;
      if (_suppressedKeys.Contains(key))
      {
        return (IntPtr)1;
      }
    }
    return CallNextHookEx(_hookID, nCode, wParam, lParam);
  }

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(IntPtr hhk);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr GetModuleHandle(string lpModuleName);
}