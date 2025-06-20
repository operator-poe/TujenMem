using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImGuiNET;
using ExileCore;
using System.Numerics;
using ExileCore.Shared.Nodes;

namespace TujenMem;

public static class StatisticsUI
{
  private class StatisticItem
  {
    public string Name { get; set; }
    public int TotalAmount { get; set; }
    public double ChaosValue { get; set; }
    public int WindowAppearances { get; set; }
    public double TotalChaosValue => TotalAmount * ChaosValue;
  }

  private class LeagueFileInfo
  {
    public string FileName { get; set; }
    public string DisplayName { get; set; }
    public DateTime LastModified { get; set; }
  }

  private static bool _showWindow = false;
  private static Dictionary<string, StatisticItem> _statisticItems = new();
  private static int _totalWindows = 0;
  private static int _totalItemEntries = 0;
  private static DateTime _lastReadTime = DateTime.MinValue;
  private static readonly TimeSpan _readCooldown = TimeSpan.FromSeconds(5);
  private static string _selectedLeagueFile = "";
  private static List<LeagueFileInfo> _availableLeagueFiles = new();

  private static string DataFolder => Path.Combine(TujenMem.Instance.DirectoryFullName, "Statistics");

  private static string DataFileName
  {
    get
    {
      if (!string.IsNullOrEmpty(_selectedLeagueFile))
      {
        return Path.Combine(DataFolder, _selectedLeagueFile);
      }
      var leagueName = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
      return Path.Combine(DataFolder, $"{leagueName}.csv");
    }
  }

  private static void RefreshAvailableLeagueFiles()
  {
    _availableLeagueFiles.Clear();

    if (!Directory.Exists(DataFolder))
    {
      return;
    }

    try
    {
      var csvFiles = Directory.GetFiles(DataFolder, "*.csv")
          .Select(filePath => new LeagueFileInfo
          {
            FileName = Path.GetFileName(filePath),
            DisplayName = $"{Path.GetFileNameWithoutExtension(filePath)} ({File.GetLastWriteTime(filePath):yyyy-MM-dd HH:mm})",
            LastModified = File.GetLastWriteTime(filePath)
          })
          .OrderByDescending(file => file.LastModified)
          .ToList();

      _availableLeagueFiles = csvFiles;

      // Set default selection to current league if not already set
      if (string.IsNullOrEmpty(_selectedLeagueFile))
      {
        var currentLeagueName = TujenMem.Instance?.Settings?.League?.Value ?? "Unknown";
        var currentLeagueFile = $"{currentLeagueName}.csv";
        if (_availableLeagueFiles.Any(f => f.FileName == currentLeagueFile))
        {
          _selectedLeagueFile = currentLeagueFile;
        }
        else if (_availableLeagueFiles.Any())
        {
          _selectedLeagueFile = _availableLeagueFiles.First().FileName;
        }
      }
    }
    catch (Exception e)
    {
      DebugWindow.LogError($"Error refreshing league files: {e}");
    }
  }

  private static void ReadAndParseData(bool force = false)
  {
    if (!File.Exists(DataFileName))
    {
      _statisticItems.Clear();
      _totalWindows = 0;
      _totalItemEntries = 0;
      return;
    }

    if (!force && DateTime.Now - _lastReadTime < _readCooldown)
    {
      return;
    }

    try
    {
      var allLines = File.ReadAllLines(DataFileName).ToList();
      if (allLines.Count <= 1)
      {
        _statisticItems.Clear();
        _totalWindows = 0;
        _totalItemEntries = 0;
        return;
      }

      if (force)
      {
        var header = allLines[0];
        var dataLines = allLines.Skip(1).ToList();

        var windows = dataLines.Select(line => line.Split(';'))
            .Where(parts => parts.Length > 2)
            .GroupBy(parts => parts[1]) // Group by WindowId
            .ToList();

        var cleanedWindows = new List<string>();
        var seenContentHashes = new HashSet<string>();

        foreach (var window in windows)
        {
          if (window.Count() < 7)
          {
            continue; // Remove windows with less than 7 items
          }

          var itemsInWindow = window.Select(parts => parts[2]).OrderBy(name => name).ToList();
          var contentHash = string.Join("|", itemsInWindow);

          if (seenContentHashes.Contains(contentHash))
          {
            continue; // Deduplicate windows with exact same items
          }

          seenContentHashes.Add(contentHash);
          cleanedWindows.AddRange(window.Select(parts => string.Join(";", parts)));
        }

        if (cleanedWindows.Count != dataLines.Count)
        {
          var newContent = new List<string> { header };
          newContent.AddRange(cleanedWindows);
          File.WriteAllLines(DataFileName, newContent);
          allLines = newContent;
        }
      }

      var lines = allLines;
      _totalItemEntries = lines.Count > 1 ? lines.Count - 1 : 0;
      if (lines.Count <= 1)
      {
        _statisticItems.Clear();
        _totalWindows = 0;
        _totalItemEntries = 0;
        return;
      }

      var windowIds = new HashSet<string>();
      var itemData = new Dictionary<string, (int totalAmount, double chaosValue, HashSet<string> windowIds)>();

      for (int i = 1; i < lines.Count; i++)
      {
        var parts = lines[i].Split(';');
        if (parts.Length > 7)
        {
          var windowId = parts[1];
          var itemName = parts[2];
          if (!double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var chaosValue))
          {
            chaosValue = 0;
          }
          if (!int.TryParse(parts[7], out var amount))
          {
            amount = 1;
          }

          windowIds.Add(windowId);

          if (!itemData.ContainsKey(itemName))
          {
            itemData[itemName] = (0, chaosValue, new HashSet<string>());
          }
          var (currentAmount, _, currentWindowIds) = itemData[itemName];
          itemData[itemName] = (currentAmount + amount, chaosValue, currentWindowIds);
          itemData[itemName].windowIds.Add(windowId);
        }
      }

      _totalWindows = windowIds.Count;
      _statisticItems = itemData.ToDictionary(
          kvp => kvp.Key,
          kvp => new StatisticItem
          {
            Name = kvp.Key,
            TotalAmount = kvp.Value.totalAmount,
            ChaosValue = kvp.Value.chaosValue,
            WindowAppearances = kvp.Value.windowIds.Count
          });
      _lastReadTime = DateTime.Now;
    }
    catch (Exception e)
    {
      DebugWindow.LogError($"Error reading statistics file: {e}");
    }
  }

  public static void Render(ToggleNode showSetting)
  {
    if (!showSetting.Value)
    {
      return;
    }

    RefreshAvailableLeagueFiles();
    ReadAndParseData();

    ImGui.SetNextWindowSize(new Vector2(400, 600), ImGuiCond.FirstUseEver);
    var isVisible = showSetting.Value;
    if (ImGui.Begin("Statistics", ref isVisible))
    {
      showSetting.Value = isVisible;

      // League file selection dropdown
      ImGui.Text("Select League File:");
      ImGui.SameLine();
      TujenMemSettings.HelpMarker("Select a league file to view statistics from. Files are ordered by last modified date.");

      ImGui.PushItemWidth(ImGui.GetWindowContentRegionMax().X - ImGui.GetStyle().FramePadding.X * 2);
      if (ImGui.BeginCombo("##leagueFileSelect", _selectedLeagueFile))
      {
        foreach (var leagueFile in _availableLeagueFiles)
        {
          bool isSelected = (_selectedLeagueFile == leagueFile.FileName);
          if (ImGui.Selectable(leagueFile.DisplayName, isSelected))
          {
            _selectedLeagueFile = leagueFile.FileName;
            ReadAndParseData(true); // Force reload with new file
          }
          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }
        ImGui.EndCombo();
      }
      ImGui.PopItemWidth();

      ImGui.Separator();

      var buttonWidth = ImGui.CalcTextSize("Reload Data").X + ImGui.GetStyle().FramePadding.X * 2.0f;
      var displayModeWidth = 150f; // Aprox width for the combo box
      var totalWidth = buttonWidth + displayModeWidth + ImGui.GetStyle().ItemSpacing.X;
      ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - totalWidth);

      ImGui.PushItemWidth(displayModeWidth);
      var modes = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsDisplayMode.Values;
      var currentMode = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsDisplayMode.Value;
      if (ImGui.BeginCombo("##displayMode", currentMode))
      {
        foreach (var mode in modes)
        {
          bool isSelected = (currentMode == mode);
          if (ImGui.Selectable(mode, isSelected))
          {
            TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsDisplayMode.Value = mode;
          }
          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }
        ImGui.EndCombo();
      }
      ImGui.PopItemWidth();

      ImGui.SameLine();

      if (ImGui.Button("Reload Data"))
      {
        ReadAndParseData(true);
      }

      ImGui.Text($"Total Windows Recorded: {_totalWindows}");
      ImGui.Separator();

      if (ImGui.BeginTable("statisticsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable))
      {
        ImGui.TableSetupColumn("Item Name");
        ImGui.TableSetupColumn("Total Count");
        ImGui.TableSetupColumn("Frequency");
        ImGui.TableSetupColumn("Avg. Chaos Value");
        ImGui.TableSetupColumn("Total Value (c)");
        ImGui.TableHeadersRow();

        var itemsToDisplay = GetItemsForMode();

        foreach (var item in itemsToDisplay)
        {
          var average = _totalWindows > 0 ? (float)item.WindowAppearances / _totalWindows : 0;

          ImGui.TableNextRow();
          ImGui.TableNextColumn();
          ImGui.Text(item.Name);
          ImGui.TableNextColumn();
          ImGui.Text(item.TotalAmount.ToString());
          ImGui.TableNextColumn();
          if (average < 1 && average > 0)
          {
            ImGui.Text($"1 per {(1 / average):F0} windows");
          }
          else
          {
            ImGui.Text($"{average:F2} per window");
          }
          ImGui.TableNextColumn();
          ImGui.Text(item.ChaosValue.ToString("F2"));
          ImGui.TableNextColumn();
          ImGui.Text(item.TotalChaosValue.ToString("F2"));
        }

        ImGui.EndTable();
      }

      ImGui.Separator();
      ImGui.Text("Overall Statistics");

      if (ImGui.BeginTable("overallStatisticsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
      {
        ImGui.TableSetupColumn("Statistic");
        ImGui.TableSetupColumn("Value");
        ImGui.TableHeadersRow();

        double totalChaosValue = _statisticItems.Values.Sum(item => item.TotalChaosValue);
        double avgValuePerWindow = _totalWindows > 0 ? totalChaosValue / _totalWindows : 0;
        int uniqueItems = _statisticItems.Count;
        double avgItemsPerWindow = _totalWindows > 0 ? (double)_totalItemEntries / _totalWindows : 0;

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Total Chaos Value Recorded");
        ImGui.TableNextColumn(); ImGui.Text(totalChaosValue.ToString("F2") + "c");

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Average Chaos Value per Window");
        ImGui.TableNextColumn(); ImGui.Text(avgValuePerWindow.ToString("F2") + "c");

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Total Unique Items Seen");
        ImGui.TableNextColumn(); ImGui.Text(uniqueItems.ToString());

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text("Average Items per Window");
        ImGui.TableNextColumn(); ImGui.Text(avgItemsPerWindow.ToString("F2"));

        ImGui.EndTable();
      }
    }
    ImGui.End();
  }

  private static List<StatisticItem> GetItemsForMode()
  {
    var mode = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsDisplayMode.Value;
    switch (mode)
    {
      case "Manual":
        var manualItems = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsManualItems;
        return _statisticItems.Values
            .Where(item => manualItems.Contains(item.Name))
            .OrderBy(item => item.Name)
            .ToList();
      case "Top 30 Valuable":
        return _statisticItems.Values
            .OrderByDescending(item => item.TotalChaosValue)
            .Take(30)
            .ToList();
      case "Top 30 Rarest":
        var minValue = TujenMem.Instance.Settings.SillyOrExperimenalFeatures.StatisticsRarestMinValue.Value;
        return _statisticItems.Values
            .Where(item => item.ChaosValue >= minValue)
            .OrderBy(item => item.WindowAppearances)
            .ThenBy(item => item.Name)
            .Take(30)
            .ToList();
      case "All":
        return _statisticItems.Values
            .OrderBy(item => item.Name)
            .ToList();
      default:
        return new List<StatisticItem>();
    }
  }

  public static void ToggleShow()
  {
    _showWindow = !_showWindow;
  }

  public static void SetShow(bool show)
  {
    _showWindow = show;
  }
}