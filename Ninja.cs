using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ImGuiNET;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TujenMem;


public enum DownloadType
{
    Currency,
    Items
}
public enum DownloadIntegrity
{
    Valid,
    Invalid,
    Unknown
}

public class Ninja
{
    private static Dictionary<string, List<NinjaItem>> _items = new();
    private static bool _dirty = true;
    private static Dictionary<string, float> fileProgress = new Dictionary<string, float>();
    private static Dictionary<string, DownloadIntegrity> fileIntegrity = new Dictionary<string, DownloadIntegrity>();

    private static string DataFolder
    {
        get
        {
            return Path.Combine(TujenMem.Instance.DirectoryFullName, "NinjaData");
        }
    }

    public static Dictionary<string, List<NinjaItem>> Items
    {
        get
        {
            if (_dirty)
            {
                Task.Run(Parse).Wait();
                _dirty = false;
            }
            return _items;
        }
    }

    public static void SetDirty()
    {
        _dirty = true;
    }

    private static readonly List<(string, DownloadType)> DownloadList = new List<(string, DownloadType)>
    {
        ("Currency", DownloadType.Currency),
        ("Fragment", DownloadType.Currency),
        ("Oil", DownloadType.Items),
        ("Incubator", DownloadType.Items),
        ("Map", DownloadType.Items),
        ("BlightedMap", DownloadType.Items),
        ("UniqueMap", DownloadType.Items),
        ("DeliriumOrb", DownloadType.Items),
        ("Scarab", DownloadType.Items),
        ("Fossil", DownloadType.Items),
        ("Resonator", DownloadType.Items),
        ("Essence", DownloadType.Items),
        ("SkillGem", DownloadType.Items),
        ("Tattoo", DownloadType.Items),
        ("DivinationCard", DownloadType.Items),
        ("ClusterJewel", DownloadType.Items)
    };

    public static bool IsValid
    {
        get
        {
            return fileIntegrity.Count == DownloadList.Count && fileIntegrity.Values.All(x => x == DownloadIntegrity.Valid);
        }
    }

    public static void RenderSettings()
    {
        if (ImGui.TreeNodeEx("Ninja Data"))
        {
            ValidityIndicator();

            if (ImGui.Button("Download Data"))
            {
                Task.Run(DownloadFilesAsync).ContinueWith((t) => { _dirty = true; CheckIntegrity(); }).ContinueWith(async (t) => { await Parse(); });
            }
            ImGui.SameLine();
            if (ImGui.Button("Re-Check Integrity"))
            {
                CheckIntegrity();
                _dirty = true;
                Task.Run(Parse);
            }

            var itemText = $"{Items.Count} items loaded";
            float textWidth = ImGui.CalcTextSize(itemText).X;
            float remainingWidth = ImGui.GetContentRegionAvail().X - textWidth;
            ImGui.SameLine(remainingWidth);
            ImGui.Text(itemText);

            if (ImGui.BeginTable("File Table", 5))
            {
                // Table headers
                ImGui.TableSetupColumn("File Name");
                ImGui.TableSetupColumn("Exists");
                ImGui.TableSetupColumn("Integrity");
                ImGui.TableSetupColumn("Age");
                ImGui.TableSetupColumn("DownloadProgress");
                ImGui.TableHeadersRow();

                // Table rows
                foreach (var file in DownloadList)
                {
                    var filePath = GetFilePathForName(file.Item1);
                    var fileName = Path.GetFileName(filePath);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(fileName);

                    ImGui.TableNextColumn();
                    var fileExists = File.Exists(filePath);
                    if (fileExists)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Yes");
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "No");
                    }


                    ImGui.TableNextColumn();
                    if (fileIntegrity.ContainsKey(filePath))
                    {
                        switch (fileIntegrity[filePath])
                        {
                            case DownloadIntegrity.Valid:
                                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Valid");
                                break;
                            case DownloadIntegrity.Invalid:
                                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Invalid");
                                break;
                            case DownloadIntegrity.Unknown:
                                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Unknown");
                                break;
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Unknown");
                    }

                    ImGui.TableNextColumn();
                    if (fileExists)
                    {
                        DateTime lastModified = File.GetLastWriteTime(filePath);
                        TimeSpan age = DateTime.Now - lastModified;

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

                        ImGui.Text(ageText);
                    }
                    else
                    {
                        ImGui.Text("-");
                    }


                    ImGui.TableNextColumn();
                    var progress = fileProgress.ContainsKey(filePath) ? fileProgress[filePath] : 0;
                    ImGui.ProgressBar(progress);
                }

                // End table
                ImGui.EndTable();
            }

            ImGui.TreePop();
        }
        else
        {
            ValidityIndicator();
        }
    }

    private static void ValidityIndicator()
    {
        ImGui.SameLine();
        ImGui.Text("Validity: ");
        ImGui.SameLine();
        if (IsValid)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Valid");
        }
        else
        {
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Invalid");
            }
        }
    }

    public static async Task Parse()
    {
        _items.Clear();


        _dirty = false;
        if (!IsValid)
        {
            return;
        }
        var result = new List<NinjaItem>();

        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            switch (dl.Item2)
            {
                case DownloadType.Currency:
                    var parsedCurrency = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONCurrencyLine>>(File.ReadAllText(filePath));
                    var linesCurrency = parsedCurrency.Lines.Select(l => new NinjaItem(l.CurrencyTypeName, l.ChaosEquivalent)).ToList();
                    result.AddRange(linesCurrency);
                    break;

                case DownloadType.Items:
                    var content = await File.ReadAllTextAsync(filePath);
                    if (dl.Item1 == "Map" || dl.Item1 == "UniqueMap")
                    {
                        var isUnique = dl.Item1 == "UniqueMap";
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineMap>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemMap(l.Name, l.ChaosValue, l.MapTier, isUnique)).ToList();
                        result.AddRange(lines);
                    }
                    else if (dl.Item1 == "SkillGem")
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineGem>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemGem(l.Name, l.ChaosValue, l.GemLevel, l.GemQuality, l.Name == "Enlighten Support" || l.Name == "Empower Support" || l.Name == "Enhance Support", l.Corrupted)).ToList();
                        result.AddRange(lines);

                    }
                    else if (dl.Item1 == "ClusterJewel")
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLineClusteJewel>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItemClusterJewel(l.Name, l.ChaosValue, l.LevelRequired, int.Parse(Regex.Replace(l.Variant, "[^0-9]", "")), l.BaseType)).ToList();
                        result.AddRange(lines);
                    }
                    else
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<JSONFile<JSONItemLine>>(content);
                        var lines = parsed.Lines.Select(l => new NinjaItem(l.Name, l.ChaosValue)).ToList();
                        result.AddRange(lines);
                    }
                    break;

            }
        }

        _items = NinjaItemListToDict(result);
    }

    private static Dictionary<string, List<NinjaItem>> NinjaItemListToDict(List<NinjaItem> items)
    {
        try
        {
            var dict = items.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var pr in TujenMem.Instance.Settings.CustomPrices)
            {
                var customPrice = pr.Item2 != null ? new CustomPrice(pr.Item1, pr.Item2 ?? 0f) : new CustomPrice(pr.Item1, pr.Item3);
                var item = dict.ContainsKey(customPrice.Name) ? dict[customPrice.Name].First() : new NinjaItem(customPrice.Name, 0);
                if (customPrice.Value != null)
                {
                    item.ChaosValue = (float)customPrice.Value;
                }
                else
                {
                    try
                    {
                        string replacedExpr = dict.Aggregate(customPrice.Expression, (current, pair) => current.Replace($"{{{pair.Key}}}", pair.Value.First().ChaosValue.ToString(CultureInfo.InvariantCulture)));
                        float result = Evaluate(replacedExpr);
                        item.ChaosValue = result;
                    }
                    catch (Exception e)
                    {
                        Log.Error(customPrice.Name + ":" + customPrice.Expression + " - " + e.Message);
                    }
                }
                dict[customPrice.Name] = new List<NinjaItem> { item };
            }
            return dict;
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            return new();
        }
    }
    private static float Evaluate(string expression)
    {
        DataTable table = new();
        table.Columns.Add("expression", typeof(string), expression);
        DataRow row = table.NewRow();
        table.Rows.Add(row);
        return float.Parse((string)row["expression"]);
    }

    public static void CheckIntegrity()
    {
        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            if (!File.Exists(filePath))
            {
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
                continue;
            }
            try
            {
                string text = File.ReadAllText(filePath);
                JToken.Parse(text);
                fileIntegrity[filePath] = DownloadIntegrity.Valid;
            }
            catch
            {
                fileIntegrity[filePath] = DownloadIntegrity.Invalid;
            }
        }
    }

    private static async Task DownloadFileAsync(string url, string filePath)
    {
        using (HttpClient client = new HttpClient())
        using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(filePath, FileMode.Create))
        {
            var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
            var buffer = new byte[8192];
            var bytesRead = 0L;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, (int)bytesRead);
                fileProgress[filePath] = (float)(fileStream.Length * 1.0 / totalBytes);
            }
        }
    }

    private static async Task DownloadFilesAsync()
    {
        if (!Directory.Exists(DataFolder))
        {
            Directory.CreateDirectory(DataFolder);
        }
        List<Task> tasks = new List<Task>();
        foreach (var dl in DownloadList)
        {
            var filePath = GetFilePathForName(dl.Item1);
            var url = GetUrlForDownloadFile(dl);
            fileProgress[filePath] = 0;
            tasks.Add(DownloadFileAsync(url, filePath));
        }
        await Task.WhenAll(tasks);
    }

    private static string GetFilePathForName(string name)
    {
        return Path.Join(DataFolder, name + ".json");
    }

    private static string GetUrlForDownloadFile((string, DownloadType) dl)
    {
        var league = TujenMem.Instance.Settings.League;
        switch (dl.Item2)
        {
            case DownloadType.Currency:
                return "https://poe.ninja/api/data/CurrencyOverview?league=" + league + "&type=" + dl.Item1 + "&language=en";
            case DownloadType.Items:
                return "https://poe.ninja/api/data/ItemOverview?league=" + league + "&type=" + dl.Item1 + "&language=en";
            default:
                throw new Exception("Unknown DownloadType");
        }
    }

}