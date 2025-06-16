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
    string cleanedText = GetCleanClipboardText();
    if (string.IsNullOrEmpty(cleanedText))
    {
      return;
    }

    // --- Information Extraction ---

    Regex regex = new Regex(@"(?:(\d+)\s*(?:x|stock|pcs?|pieces?|units?)|(?:x|stock|pcs?|pieces?|units?)\s*(\d+))\s*.*?(?:Black\s+Scythe|Scythe\s+Black).*?(?:(\d+)\s*(?:c|chaos|С|:chaos:)|(?:c|chaos|С|:chaos:)\s*(\d+))", RegexOptions.IgnoreCase);
    MatchCollection matches = regex.Matches(cleanedText);

    foreach (Match match in matches)
    {
      var quantity = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
      var price = match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value;

      string ign = null;
      Regex nameRegex = new Regex(@"IGN:?\s*(.*?)(?:\s|$)", RegexOptions.IgnoreCase);
      Match nameMatch = nameRegex.Match(cleanedText);

      if (nameMatch.Success)
      {
        ign = nameMatch.Groups[1].Value.Trim();
      }
      else
      {
        var words = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words.Reverse())
        {
          if (word.Contains("@") || (word.Length >= 3 && !word.Any(char.IsDigit)))
          {
            ign = word.Trim().Replace("@", "");
            break;
          }
        }
      }

      if (string.IsNullOrEmpty(ign) || LastOffer?.Ign == ign)
      {
        return;
      }

      int quantityInt, priceInt;
      if (!int.TryParse(quantity, out quantityInt) || !int.TryParse(price, out priceInt))
      {
        // Don't crash, just skip this match
        continue;
      }

      CurrentOffer = new LogbookOffer
      {
        Quantity = quantityInt,
        Price = priceInt,
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
    var show = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value;
    ImGui.Begin("Tujen Buy Assistance", ref show);
    TujenMem.Instance.Settings.SillyOrExperimenalFeatures.EnableBuyAssistance.Value = show;

    // Debug Panel
    if (ImGui.CollapsingHeader("Debug Information"))
    {
      string cleanedText = GetCleanClipboardText();
      if (!string.IsNullOrEmpty(cleanedText))
      {
        ImGui.TextWrapped("Cleaned Text:");
        ImGui.TextWrapped(cleanedText);
        ImGui.Separator();

        // Show regex matches
        Regex regex = new Regex(@"(?:(\d+)\s*(?:x|stock|pcs?|pieces?|units?)|(?:x|stock|pcs?|pieces?|units?)\s*(\d+))\s*.*?(?:Black\s+Scythe|Scythe\s+Black).*?(?:(\d+)\s*(?:c|chaos|С|:chaos:)|(?:c|chaos|С|:chaos:)\s*(\d+))", RegexOptions.IgnoreCase);
        MatchCollection matches = regex.Matches(cleanedText);

        ImGui.Text($"Found {matches.Count} matches");
        for (int i = 0; i < matches.Count; i++)
        {
          var match = matches[i];
          ImGui.Text($"Match {i + 1}:");
          ImGui.Indent(20);
          ImGui.Text($"Full match: {match.Value}");
          ImGui.Text($"Quantity (Group 1): {match.Groups[1].Value}");
          ImGui.Text($"Quantity (Group 2): {match.Groups[2].Value}");
          ImGui.Text($"Price (Group 3): {match.Groups[3].Value}");
          ImGui.Text($"Price (Group 4): {match.Groups[4].Value}");
          ImGui.Unindent(20);
        }
        ImGui.Separator();

        // Show IGN detection
        ImGui.Text("IGN Detection:");
        ImGui.Indent(20);
        Regex nameRegex = new Regex(@"IGN:?\s*(.*?)(?:\s|$)", RegexOptions.IgnoreCase);
        Match nameMatch = nameRegex.Match(cleanedText);

        if (nameMatch.Success)
        {
          ImGui.Text($"Found IGN via pattern: {nameMatch.Groups[1].Value.Trim()}");
        }
        else
        {
          ImGui.Text("No IGN found via pattern, trying fallback...");
          var words = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
          bool foundIgn = false;
          foreach (var word in words.Reverse())
          {
            if (word.Contains("@") || (word.Length >= 3 && !word.Any(char.IsDigit)))
            {
              ImGui.Text($"Found potential IGN via fallback: {word.Trim().Replace("@", "")}");
              foundIgn = true;
              break;
            }
          }
          if (!foundIgn)
          {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No IGN found in text!");
          }
        }
        ImGui.Unindent(20);
      }
      else
      {
        ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No text in clipboard or text doesn't contain 'Black' or 'Scythe'");
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