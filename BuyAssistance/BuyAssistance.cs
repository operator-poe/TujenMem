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
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace TujenMem.BuyAssistance;

public class BuyAssistance
{
  public static LogbookOffer CurrentOffer { get; set; } = null;
  public static LogbookOffer LastOffer { get; set; }

  // A dictionary to map common homoglyphs to their standard ASCII counterparts.
  // This is more scalable and maintainable than a long chain of .Replace() calls.
  private static readonly Dictionary<char, char> HomoglyphMap = new Dictionary<char, char>
    {
        // Cyrillic letters that look like Latin letters
        { 'А', 'A' }, // Cyrillic A -> Latin A
        { 'а', 'a' },
        { 'Е', 'E' }, // Cyrillic E -> Latin E
        { 'е', 'e' },
        { 'О', 'O' }, // Cyrillic O -> Latin O
        { 'о', 'o' },
        { 'Р', 'P' }, // Cyrillic R -> Latin P
        { 'р', 'p' },
        { 'С', 'C' }, // Cyrillic S -> Latin C
        { 'с', 'c' },
        { 'Х', 'X' }, // Cyrillic Ha -> Latin X
        { 'х', 'x' },
        // Add other common look-alikes here as needed
        { 'Ι', 'I' }, // Greek Iota -> Latin I
        { 'І', 'I' }, // Cyrillic I -> Latin I
    };

  private static readonly Stopwatch _clipboardStopwatch = new Stopwatch();
  private static string _cachedClipboardText = string.Empty;
  private static string _lastClipboardTextForCurrentOffer = string.Empty;
  private const int ClipboardDebounceTimeMs = 250;

  private static void UpdateClipboardCache()
  {
    if (!_clipboardStopwatch.IsRunning)
    {
      _clipboardStopwatch.Start();
    }
    if (_clipboardStopwatch.ElapsedMilliseconds < ClipboardDebounceTimeMs)
    {
      return;
    }

    _clipboardStopwatch.Restart();
    _cachedClipboardText = GetClipboardText(); // The original, heavy method
  }

  /// <summary>
  /// Replaces common homoglyphs (characters that look similar) using a predefined map.
  /// This is a crucial step for cleaning text before regex matching.
  /// </summary>
  /// <param name="text">The input string.</param>
  /// <returns>A string with homoglyphs replaced by their standard ASCII equivalents.</returns>
  public static string ReplaceHomoglyphs(string text)
  {
    var stringBuilder = new StringBuilder(text.Length);
    foreach (char c in text)
    {
      // If the character is in our map, append the replacement. Otherwise, append the original.
      stringBuilder.Append(HomoglyphMap.TryGetValue(c, out char replacement) ? replacement : c);
    }
    return stringBuilder.ToString();
  }

  public static string GetCleanClipboardText()
  {
    var clipboardText = GetClipboardText();
    if (string.IsNullOrEmpty(clipboardText))
    {
      return string.Empty;
    }
    return GetCleanText(clipboardText);
  }

  public static string GetCleanText(string text)
  {
    // Stage 1: Replace known non-standard characters and homoglyphs
    var homoglyphText = ReplaceHomoglyphs(text);

    // Stage 2: Normalize the text to FormKC (compatibility decomposition + composition)
    var normalizedText = homoglyphText.Normalize(NormalizationForm.FormKC);

    // Stage 3: Process each character individually
    var stringBuilder = new StringBuilder();
    foreach (char c in normalizedText)
    {
      // Skip control characters except for basic whitespace
      if (char.IsControl(c) && c != ' ' && c != '\t' && c != '\n' && c != '\r')
        continue;

      // Replace any whitespace character with a regular space
      if (char.IsWhiteSpace(c))
      {
        stringBuilder.Append(' ');
        continue;
      }

      // Only keep printable ASCII characters and common UTF-8 characters
      if (c < 128 || (c >= 0x0080 && c <= 0xFFFF))
      {
        stringBuilder.Append(c);
      }
    }

    // Stage 4: Clean up multiple spaces
    var cleanedText = stringBuilder.ToString();
    cleanedText = Regex.Replace(cleanedText, @"\s+", " ").Trim();

    // Remove stray replacement characters (question marks)
    cleanedText = cleanedText.Replace("?", "");

    return cleanedText;
  }

  public static void Tick()
  {
    UpdateClipboardCache();
    var clipboardText = _cachedClipboardText;

    if (string.IsNullOrEmpty(clipboardText))
    {
      return;
    }

    // If the clipboard text is the same one that generated the current offer,
    // don't do anything. This prevents overwriting user modifications.
    if (CurrentOffer != null && clipboardText == _lastClipboardTextForCurrentOffer)
    {
      return;
    }

    var (ign, quantity, price) = ParseOffer.ExtractOffer(clipboardText);

    if (string.IsNullOrEmpty(ign) || (LastOffer != null && LastOffer.Ign == ign))
    {
      return;
    }

    // We have a valid, new offer. Create it and store the source text.
    CurrentOffer = new LogbookOffer
    {
      Quantity = quantity,
      Price = price,
      Ign = ign
    };
    _lastClipboardTextForCurrentOffer = clipboardText;
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
    var show = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value;
    ImGui.Begin("Tujen Buy Assistance", ref show);
    TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value = show;

    // Debug Panel
    if (ImGui.CollapsingHeader("Debug Information"))
    {
      var clipboardText = _cachedClipboardText;
      if (!string.IsNullOrEmpty(clipboardText))
      {
        ImGui.TextWrapped("Raw Clipboard Text:");
        ImGui.TextWrapped(clipboardText);
        ImGui.Separator();

        var cleanedText = ParseOffer.CleanInput(clipboardText);
        ImGui.TextWrapped("Cleaned Text (for parsing):");
        ImGui.TextWrapped(cleanedText);
        ImGui.Separator();

        ImGui.Text("Parsing Results:");
        ImGui.Indent(20);
        var (ign, quantity, price) = ParseOffer.ExtractOffer(clipboardText);
        if (!string.IsNullOrEmpty(ign))
        {
          ImGui.Text($"IGN: {ign}");
          ImGui.Text($"Quantity: {quantity}");
          ImGui.Text($"Price: {price}");
        }
        else
        {
          ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Could not parse offer from text.");
        }
        ImGui.Unindent(20);
      }
      else
      {
        ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No relevant offer text in clipboard.");
      }
    }

    if (CurrentOffer == null)
    {
      ImGui.End();
      return;
    }

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
      ImGui.SetNextItemWidth(100);
      var quantity = CurrentOffer.Quantity;
      if (ImGui.InputInt("##quantity", ref quantity))
      {
        CurrentOffer.Quantity = quantity;
      }

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