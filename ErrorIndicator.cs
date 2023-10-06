using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ImGuiNET;

namespace TujenMem;

public enum ErrorType
{
    StartUp,
}

public enum ErrorMessage
{
    NinjaValidity,
}

public class ErrorIndicator
{
    private static bool showErrorModal = false;

    private static List<(string, string)> errorList = new List<(string, string)>();

    public static void AddError(string errorType, string errorMessage)
    {
        errorList.Add((errorType, errorMessage));
    }

    public static void ShowError()
    {
        showErrorModal = true;
    }


    public static void Render()
    {
        // Open the popup if an error occurs
        if (showErrorModal)
        {
            ImGui.OpenPopup("TujenMem Alert");
        }

        // Error modal styling
        bool modalOpen = true;
        ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.7f));
        if (ImGui.BeginPopupModal("TujenMem Alert", ref modalOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            foreach (var e in errorList)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0, 1), (e.Item1));
                ImGui.Text((e.Item2));
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            if (ImGui.Button("Close", new System.Numerics.Vector2(120, 0)))
            {
                showErrorModal = false;
                errorList.Clear();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.PopStyleColor();
    }

}