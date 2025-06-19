using System.Threading;
using System.Windows.Forms;
using ExileCore.Shared;

namespace TujenMem;

public class ClipUtil
{
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

  public static string GetClipboardText()
  {
    string result = string.Empty;
    var thread = new Thread(() =>
    {
      result = Clipboard.GetText();
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    return result;
  }

  public static async SyncTask<bool> CopyWithVerification(int timeoutMs = 1000)
  {
    // Clear clipboard first to ensure we can detect the new text
    ClearClipboard();

    // Perform Ctrl+C
    await InputAsync.KeyDown(Keys.ControlKey);
    await InputAsync.KeyPress(Keys.C);
    await InputAsync.KeyUp(Keys.ControlKey);

    // Wait for clipboard to be populated with new text
    return await InputAsync.Wait(() => !string.IsNullOrEmpty(GetClipboardText()), timeoutMs, "Clipboard copy failed - no text detected");
  }
}