using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;

namespace TujenMem;

public class ChatPointer
{
    public string Pointer { get; set; }
    public int Index { get; set; }
}
public static class Chat
{
    public static TujenMem Instance = TujenMem.Instance;

    public static ChatPanel Panel
    {
        get
        {
            return TujenMem.Instance.GameController.Game.IngameState.IngameUi.ChatPanel;
        }
    }

    public static bool IsOpen
    {
        get
        {
            return Panel.ChatInputElement.IsVisible;
        }
    }

    public static string CurrentInput
    {
        get
        {
            return Panel.InputText;
        }
    }

    public static void SendChatMessage(string message)
    {
        Instance.Scheduler.AddTask(Send([message]), "Chat.SendChatMessage");
    }
    public static void SendChatMessage(string[] message)
    {
        Instance.Scheduler.AddTask(Send(message), "Chat.SendChatMessage");
    }

    public static async SyncTask<bool> Clear()
    {
        await Open();

        if (CurrentInput == null || CurrentInput == "")
            return true;


        // second method: select all and delete
        await InputAsync.KeyDown(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.A);
        await InputAsync.KeyUp(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.Delete);

        await InputAsync.Wait(() => CurrentInput == null, 100, "Chat input not cleared (2)");

        // third method: just backspace until empty
        while (CurrentInput != null)
        {
            await InputAsync.KeyPress(Keys.Back);
        }
        await InputAsync.Wait(() => CurrentInput == null, 100, "Chat input not cleared (3)");
        return true;
    }

    public static async SyncTask<bool> Replace()
    {
        await Open();

        if (CurrentInput != null && CurrentInput != "")
        {
            await InputAsync.KeyDown(Keys.ControlKey);
            await InputAsync.KeyPress(Keys.A);
            await InputAsync.KeyUp(Keys.ControlKey);
        }
        return true;
    }

    public static async SyncTask<bool> Repeat(string message)
    {
        await Open();

        await InputAsync.KeyPress(Keys.Up);
        await InputAsync.Wait(() => CurrentInput == message, 100);
        if (CurrentInput == message)
        {
            await InputAsync.KeyPress(Keys.Enter);
            return true;
        }
        else
        {
            await Send([message]);
            return false;
        }
    }

    public static async SyncTask<bool> Send(string[] messages, bool replace = true)
    {
        foreach (var message in messages)
        {
            await Open();
            if (replace)
                await Replace();
            else
                await Clear();

            Util.SetClipBoardText(message);
            await InputAsync.Wait(() => Util.GetClipboardText() == message, 1000, "Clipboard text not set");
            await InputAsync.KeyDown(Keys.ControlKey);
            await InputAsync.KeyPress(Keys.V);
            await InputAsync.KeyUp(Keys.ControlKey);

            await InputAsync.Wait(() => CurrentInput == message, 1000, "Chat input not set to message");

            await InputAsync.KeyPress(Keys.Enter);

            await InputAsync.Wait(() => !IsOpen, 1000, "Chat window not closed");
        }
        return true;
    }

    public static void ChatWith(string username)
    {
        Instance.Scheduler.AddTask(ChatWithUser(username), "Chat.ChatWithUser");
    }

    public static async SyncTask<bool> ChatWithUser(string username)
    {
        await Open();
        await Replace();

        Util.SetClipBoardText($"@{username} ");
        await InputAsync.Wait(() => Util.GetClipboardText() == $"@{username} ", 1000, "Clipboard text not set");
        await InputAsync.KeyDown(Keys.ControlKey);
        await InputAsync.KeyPress(Keys.V);
        await InputAsync.KeyUp(Keys.ControlKey);

        await InputAsync.Wait(() => CurrentInput == $"@{username} ", 1000, "Chat input not set to message");
        return true;
    }

    public static async SyncTask<bool> Open()
    {
        await Util.ForceFocusAsync();
        if (!IsOpen)
        {
            await InputAsync.KeyPress(Keys.Enter);
        }

        return await InputAsync.Wait(() => IsOpen, 1000, "Cannot open chat");
    }
}