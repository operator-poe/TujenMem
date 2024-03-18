using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using ExileCore;
using ExileCore.Shared;
using ImGuiNET;
using TujenMem.PrepareLogbook;

namespace TujenMem.BuyAssistance;

public class BuyAssistance
{
  public static LogbookOffer CurrentOffer { get; set; } = null;
  public static LogbookOffer LastOffer { get; set; }

  public static void Tick()
  {
    string clipboardText = GetClipboardText();
    if (string.IsNullOrEmpty(clipboardText))
    {
      return;
    }
    clipboardText = clipboardText.Replace(":", "");

    Regex regex = new Regex(@"(\d+x|x\d+)\s?.*?(Scythe|Black).*?\s+(\d+)\s?c", RegexOptions.IgnoreCase);
    MatchCollection matches = regex.Matches(clipboardText);

    foreach (Match match in matches)
    {
      var quantity = match.Groups[1].Value.ToLower().Replace("x", "");
      var price = match.Groups[3].Value.ToLower().Replace("c", "");

      Regex nameRegex = new Regex(@"IGN:?\s?(.*)", RegexOptions.IgnoreCase);
      Match nameMatch = nameRegex.Match(clipboardText);

      var ign = nameMatch.Success ? nameMatch.Groups[1].Value : clipboardText.Split(' ').Last();
      ign = ign.Trim().Replace("@", "");
      if (LastOffer?.Ign == ign)
      {
        return;
      }

      CurrentOffer = new LogbookOffer
      {
        Quantity = int.Parse(quantity),
        Price = int.Parse(price),
        Ign = ign
      };

      break;
    }

  }

  [DllImport("user32.dll", SetLastError = true)]
  public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool SetForegroundWindow(IntPtr hWnd);

  public static void ForceFocus(string windowTitle)
  {
    IntPtr handle = FindWindow(null, windowTitle);
    if (handle != IntPtr.Zero)
    {
      SetForegroundWindow(handle);
    }
  }

  public static string _sendPmCoroutineName = "TujenMem_SendPmCoroutine";
  private static IEnumerator SendPM()
  {
    // Focus on PoE
    if (!TujenMem.Instance.GameController.Window.IsForeground())
    {
      ForceFocus("Path of Exile");
    }
    yield return new WaitFunctionTimed(TujenMem.Instance.GameController.Window.IsForeground, true, 1000, "Window could not be focused");

    Input.KeyDown(Keys.Enter);
    Input.KeyUp(Keys.Enter);
    yield return new WaitFunctionTimed(() => TujenMem.Instance.GameController.IngameState.IngameUi.ChatBox is { IsVisible: true }, true, 1000, "Chat window not opened");

    Input.KeyDown(Keys.ControlKey);
    Input.KeyDown(Keys.A);
    Input.KeyUp(Keys.A);
    Input.KeyUp(Keys.ControlKey);
    yield return new WaitTime(50);
    Input.KeyDown(Keys.Delete);
    Input.KeyUp(Keys.Delete);
    yield return new WaitTime(50);

    var divines = CurrentOffer.Divines;
    var chaos = CurrentOffer.Chaos;
    var priceText = divines > 0 ? $"{divines}d, {chaos}c" : $"{chaos}c";
    var text = $"@{CurrentOffer.Ign} Hi, I'd like to buy your {CurrentOffer.Quantity} Black Scythe Logbooks for {priceText}. Thanks!";
    SetClipBoardText(text);
    yield return new WaitFunctionTimed(() => GetClipboardText() == text, true, 1000, "Clipboard text not set");
    Input.KeyDown(Keys.ControlKey);
    Input.KeyDown(Keys.V);
    Input.KeyUp(Keys.V);
    Input.KeyUp(Keys.ControlKey);
    yield return new WaitTime(50);

    yield return Input.KeyPress(Keys.Enter);
    yield return new WaitFunctionTimed(() => TujenMem.Instance.GameController.IngameState.IngameUi.ChatBox is { IsVisible: false }, true, 1000, "Chat window not closed");
  }

  public static string _extractMoneyFromStashCoroutineName = "TujenMem_ExtractMoneyFromStashCoroutine";
  private static IEnumerator ExtractMoneyFromStash()
  {
    // Focus on PoE
    if (!TujenMem.Instance.GameController.Window.IsForeground())
    {
      ForceFocus("Path of Exile");
    }
    yield return new WaitFunctionTimed(TujenMem.Instance.GameController.Window.IsForeground, true, 1000, "Window could not be focused");

    var d = PrepareLogbook.Inventory.DivineCount;

    var stash = new Stash();
    while (PrepareLogbook.Inventory.DivineCount < CurrentOffer.Divines)
    {
      var rest = CurrentOffer.Divines - PrepareLogbook.Inventory.DivineCount;
      if (rest > 10)
      {
        yield return stash.Divine.GetStack();
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
      }
      else
      {
        var p = PrepareLogbook.Inventory.NextFreePosition;
        yield return stash.Divine.GetFraction(rest);
        Input.SetCursorPos(p);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
      }
      yield return new WaitTime(50);
    }
    while (PrepareLogbook.Inventory.ChaosCount < CurrentOffer.Chaos)
    {
      var rest = CurrentOffer.Chaos - PrepareLogbook.Inventory.ChaosCount;
      if (rest > 20)
      {
        yield return stash.Chaos.GetStack();
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
      }
      else
      {
        var p = PrepareLogbook.Inventory.NextFreePosition;
        yield return stash.Chaos.GetFraction(rest);
        Input.SetCursorPos(p);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(TujenMem.Instance.Settings.HoverItemDelay);
      }
      yield return new WaitTime(50);
    }
  }


  public static void Render()
  {
    if (CurrentOffer == null)
    {
      return;
    }
    var show = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value;
    ImGui.Begin("Tujen Buy Assistance", ref show);
    TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value = show;

    if (ImGui.BeginTable("BuyAssistanceTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
      ImGui.TableSetupColumn("IGN");
      ImGui.TableSetupColumn("Amount");
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Total");
      ImGui.TableSetupColumn("PM");
      ImGui.TableSetupColumn("$$$");
      ImGui.TableSetupColumn("X");
      ImGui.TableHeadersRow();

      ImGui.TableNextRow();

      ImGui.TableNextColumn();
      ImGui.Text(CurrentOffer.Ign);

      ImGui.TableNextColumn();
      ImGui.Text(CurrentOffer.Quantity.ToString());

      ImGui.TableNextColumn();
      ImGui.Text(CurrentOffer.Price.ToString());

      ImGui.TableNextColumn();

      ImGui.Text($"{CurrentOffer.Divines}d\n{CurrentOffer.Chaos}c");

      ImGui.TableNextColumn();
      if (ImGui.Button("PM"))
      {
        if (Core.ParallelRunner.FindByName(_sendPmCoroutineName) == null)
        {
          Core.ParallelRunner.Run(new Coroutine(SendPM(), TujenMem.Instance, _sendPmCoroutineName));
        }
      }

      ImGui.TableNextColumn();
      if (ImGui.Button("$$$"))
      {
        if (Core.ParallelRunner.FindByName(_extractMoneyFromStashCoroutineName) == null)
        {
          Core.ParallelRunner.Run(new Coroutine(ExtractMoneyFromStash(), TujenMem.Instance, _extractMoneyFromStashCoroutineName));
        }
      }

      ImGui.TableNextColumn();
      if (ImGui.Button("X"))
      {
        // Handle button click event
        LastOffer = CurrentOffer;
        CurrentOffer = null;
        ImGui.End();
        return;
      }

    }
    ImGui.EndTable();
    ImGui.End();
  }

  public static string GetClipboardText()
  {
    string result = string.Empty;
    Thread staThread = new Thread(() =>
    {
      result = Clipboard.GetText();
    });
    staThread.SetApartmentState(ApartmentState.STA);
    staThread.Start();
    staThread.Join();
    if (!result.ToLower().Contains("black") && !result.ToLower().Contains("scythe"))
    {
      return string.Empty;
    }
    return result;
  }

  public static void SetClipBoardText(string text)
  {
    var thread = new Thread(() =>
    {
      Clipboard.SetText(text);
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
  }
}