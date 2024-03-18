using System.Collections.Generic;
using System.Text;
using ExileCore.PoEMemory;
using ImGuiNET;

namespace TujenMem;

public class Error
{
    public static bool IsDisplaying
    {
        get
        {
            return showErrorModal;
        }
    }
    private static bool showErrorModal = false;

    private static List<(string, string)> errorList = new List<(string, string)>();

    public static void Add(string errorType, string errorMessage)
    {
        errorList.Add((errorType, errorMessage));
        Log.Error($"{errorType}: {errorMessage}");
    }

    public static void AddAndShow(string errorType, string errorMessage)
    {
        Add(errorType, errorMessage);
        Show();
    }

    public static void Show()
    {
        showErrorModal = true;
        if (TujenMem.Instance.IsAnyRoutineRunning)
        {
            TujenMem.Instance.StopAllRoutines();
        }
    }

    public static void ShowIfNeeded()
    {
        if (errorList.Count > 0)
        {
            Show();
        }
    }

    public static void Clear()
    {
        showErrorModal = false;
        errorList.Clear();
    }


    public static void Render()
    {
        if (showErrorModal)
        {
            ImGui.OpenPopup("TujenMem Alert");
        }

        bool modalOpen = true;
        System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.7f));
        if (ImGui.BeginPopupModal("TujenMem Alert", ref modalOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            System.Numerics.Vector2 windowPos = ImGui.GetWindowPos();
            System.Numerics.Vector2 windowSize = ImGui.GetWindowSize();
            ImGui.GetWindowDrawList().AddRectFilled(windowPos, new System.Numerics.Vector2(windowPos.X + windowSize.X, windowPos.Y + 30), ImGui.GetColorU32(ImGuiCol.Header));
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 1, 1), "TujenMem Alert");
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8);
            ImGui.Separator();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8);


            foreach (var e in errorList)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0, 1), e.Item1);
                ImGui.Text(e.Item2);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            float buttonWidth = 120.0f;
            ImGui.SetCursorPosX((windowSize.X - buttonWidth) * 0.5f);

            if (ImGui.Button("OK", new System.Numerics.Vector2(buttonWidth, 0)))
            {
                Clear();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.PopStyleColor();
    }

    public static string VisualizeElementTree(Element node, int depth = 0)
    {
        StringBuilder sb = new StringBuilder();
        string displayText = node.Text != null ? node.Text : $"({node.GetType().Name})";

        // Create prefix based on depth
        string prefix = new string(' ', depth * 2);
        if (node.IndexInParent >= 0)
        {
            displayText = $"[{node.IndexInParent}] {displayText}";
        }
        sb.AppendLine(prefix + "|-" + displayText);

        for (int i = 0; i < node.Children.Count; i++)
        {
            sb.Append(VisualizeElementTree(node.Children[i], depth + 1));
        }

        return sb.ToString();
    }

}