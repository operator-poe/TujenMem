using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ExileCore.Shared;

namespace TujenMem;

public static class Util
{
    public static Keys MapIndexToNumPad(int index)
    {
        switch (index)
        {
            case 0:
                return Keys.NumPad0;
            case 1:
                return Keys.NumPad1;
            case 2:
                return Keys.NumPad2;
            case 3:
                return Keys.NumPad3;
            case 4:
                return Keys.NumPad4;
            case 5:
                return Keys.NumPad5;
            case 6:
                return Keys.NumPad6;
            case 7:
                return Keys.NumPad7;
            case 8:
                return Keys.NumPad8;
            case 9:
                return Keys.NumPad9;
            default:
                return Keys.None;
        }
    }

    public static int MapNumPadToIndex(Keys key)
    {
        switch (key)
        {
            case Keys.NumPad0:
                return 0;
            case Keys.NumPad1:
                return 1;
            case Keys.NumPad2:
                return 2;
            case Keys.NumPad3:
                return 3;
            case Keys.NumPad4:
                return 4;
            case Keys.NumPad5:
                return 5;
            case Keys.NumPad6:
                return 6;
            case Keys.NumPad7:
                return 7;
            case Keys.NumPad8:
                return 8;
            case Keys.NumPad9:
                return 9;
            default:
                return -1;
        }
    }

    public static int LevenshteinDistance(string s, string t)
    {
        // Special cases
        if (s == t) return 0;
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;
        // Initialize the distance matrix
        int[,] distance = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) distance[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) distance[0, j] = j;
        // Calculate the distance
        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            }
        }
        // Return the distance
        return distance[s.Length, t.Length];
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
    public static void ClearClipboard()
    {
        var thread = new Thread(() =>
        {
            Clipboard.Clear();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    public static async SyncTask<bool> ForceFocusAsync()
    {
        if (!TujenMem.Instance.GameController.Window.IsForeground())
        {
            IntPtr handle = FindWindow(null, "Path of Exile");
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        }
        return await InputAsync.Wait(() => TujenMem.Instance.GameController.Window.IsForeground(), 1000, "Window could not be focused");
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
        return result;
    }

    public static string FormatChaosPrice(float value, float? DivinePrice = null)
    {
        if (DivinePrice == null || DivinePrice <= 0 || Math.Abs(value) < DivinePrice)
            return $"{value.ToString("0", CultureInfo.InvariantCulture)}c";

        int divines = (int)(value / DivinePrice);
        float chaos = value % DivinePrice ?? 0;
        return $"{divines} div, {chaos.ToString("0", CultureInfo.InvariantCulture)}c";
    }

    public static string FormatTimeSpan(TimeSpan age)
    {
        string ageText;
        if (age.TotalDays >= 1)
        {
            ageText = $"{(int)age.TotalDays} days, {(int)age.Hours} hours";
        }
        else if (age.TotalHours >= 1)
        {
            ageText = $"{(int)age.TotalHours} hours, {age.Minutes} minutes";
        }
        else if (age.TotalMinutes >= 1)
        {
            ageText = $"{age.Minutes} minutes";
        }
        else
        {
            ageText = $"{age.Seconds} seconds";
        }
        return ageText;
    }
}

public class ThrottledAction
{
    private DateTime lastCheck = DateTime.MinValue;
    private readonly TimeSpan throttlePeriod;
    private readonly Action action;

    public ThrottledAction(TimeSpan throttlePeriod, Action actionToExecute)
    {
        this.throttlePeriod = throttlePeriod;
        this.action = actionToExecute;
    }

    public void Run()
    {
        if (DateTime.Now - lastCheck > throttlePeriod)
        {
            action?.Invoke();
            lastCheck = DateTime.Now;
        }
    }

    public void Invoke()
    {
        action?.Invoke();
    }
}
