using SharpDX;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Globalization;
using ImGuiNET;
using ExileCore.PoEMemory.Elements;
using System.IO;
using System.Media;
using ExileCore.PoEMemory.Elements.AtlasElements;

namespace TujenMem;

public class TujenMem : BaseSettingsPlugin<TujenMemSettings>
{
    internal static TujenMem Instance;

    private HaggleState _haggleState = HaggleState.Idle;

    public Dictionary<string, List<NinjaItem>> NinjaItems = new();

    private bool _areaHasVorana = false;

    public override bool Initialise()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        Input.RegisterKey(Settings.HotKeySettings.StartHotKey);
        Settings.HotKeySettings.StartHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.StartHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.StopHotKey);
        Settings.HotKeySettings.StopHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.StopHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey);
        Settings.HotKeySettings.RollAndBlessHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey); };

        Settings.FetchNinja.OnPressed += async () => await FetchPrices();
        Task.Run(async () =>
        {
            var worker = new FetchNinja(Settings.League, DirectoryFullName);
            if (worker.CheckIfShouldFetch())
            {
                var result = await worker.Fetch();
                if (result == null)
                {
                    LogMsg("Fetched Ninja");
                }
                else
                {
                    LogError("Failed to fetch Ninja. " + result);
                    return;
                }
            }
            else
            {
                LogMsg("No need to fetch Ninja");
            }
            if (worker.CheckIfCanParse)
            {
                var items = await worker.ParseNinjaItems();
                NinjaItems = NinjaItemListToDict(items);
                LogMsg("Parsed Ninja");
            }
            else
            {
                LogError("Failed to parse Ninja. Not all data is available.");
                return;
            }
        });

        return base.Initialise();
    }

    private Dictionary<string, List<NinjaItem>> NinjaItemListToDict(List<NinjaItem> items)
    {
        try
        {
            var dict = items.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var pr in Settings.CustomPrices)
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
                        LogError(customPrice.Name + ":" + customPrice.Expression + " - " + e.Message);
                    }
                }
                dict[customPrice.Name] = new List<NinjaItem> { item };
            }
            return dict;
        }
        catch (Exception e)
        {
            LogError(e.Message);
            return new();
        }
    }

    static float Evaluate(string expression)
    {
        DataTable table = new();
        table.Columns.Add("expression", typeof(string), expression);
        DataRow row = table.NewRow();
        table.Rows.Add(row);
        return float.Parse((string)row["expression"]);
    }

    public override void AreaChange(AreaInstance area)
    {
        _areaHasVorana = false;
    }

    private async Task FetchPrices()
    {
        LogMsg("Fetching Ninja");
        var worker = new FetchNinja(Settings.League, DirectoryFullName);
        if (worker.CheckIfShouldFetch())
        {
            var result = await worker.Fetch();
            if (result == null)
            {
                LogMsg("Fetched Ninja");
            }
            else
            {
                LogError("Failed to fetch Ninja. " + result);
            }
        }
        else
        {
            LogMsg("No need to fetch Ninja");
        }
    }



    private readonly static string _coroutineName = "TujenMem_Haggle";
    private readonly static string _empty_inventory_coroutine_name = "TujenMem_Inventory";
    private readonly static string _reroll_coroutine_name = "TujenMem_Reroll";
    public override Job Tick()
    {
        if (Settings.HotKeySettings.StartHotKey.PressedOnce())
        {
            if (Core.ParallelRunner.FindByName(_coroutineName) == null)
            {
                _haggleState = HaggleState.StartUp;
                Core.ParallelRunner.Run(new Coroutine(HaggleCoroutine(), this, _coroutineName));
            }
            else
            {
                _haggleState = HaggleState.Cancelling;
            }
        }
        if (Settings.HotKeySettings.RollAndBlessHotKey.PressedOnce())
        {
            if (Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless) == null)
            {
                Core.ParallelRunner.Run(new Coroutine(PrepareLogbook.Runner.RollAndBlessLogbooksCoroutine(), this, PrepareLogbook.Runner.CoroutineNameRollAndBless));
            }
            else
            {
                var routine = Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless);
                routine?.Done();
            }
        }
        if (_haggleState is HaggleState.Cancelling)
        {
            var routine = Core.ParallelRunner.FindByName(_coroutineName);
            routine?.Done();
            _haggleState = HaggleState.Idle;
        }
        if (Settings.HotKeySettings.StopHotKey.PressedOnce())
        {
            StopAllRoutines();
        }

        if (Settings.EnableBuyAssistance)
            BuyAssistance.BuyAssistance.Tick();


        return null;
    }

    public bool ShouldEmptyInventory()
    {
        var inventory = GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
        var inventoryItems = inventory.InventorySlotItems;

        foreach (var item in inventoryItems)
        {
            if (item is null)
            {
                continue;
            }
            if (item.PosX >= 10 && item.PosX < 11)
            {
                return true;
            }
        }
        return false;
    }

    public IEnumerator EmptyInventoryCoRoutine(bool _inventory_function_only = false)
    {
        LogMsg("Emptying Inventory");
        if (!_inventory_function_only)
        {
            yield return ExitAllWindows();
        }
        yield return FindAndClickStash();

        var inventory = GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
        var inventoryItems = inventory.InventorySlotItems;

        inventoryItems = inventoryItems.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();

        Input.KeyDown(Keys.ControlKey);
        foreach (var item in inventoryItems)
        {
            if (item is null || item.PosX >= 11)
            {
                continue;
            }
            Input.SetCursorPos(item.GetClientRect().Center);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.Click(MouseButtons.Left);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }
        Input.KeyUp(Keys.ControlKey);

        if (!_inventory_function_only)
        {
            yield return FindAndClickTujen();
        }

        yield return new WaitTime(Settings.HoverItemDelay * 3);
    }

    private IEnumerator FindAndClickTujen()
    {
        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var haggleWindow = GameController.IngameState.IngameUi.HaggleWindow;

        if (haggleWindow is { IsVisible: true })
        {
            var haggleWindowSub = GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow;
            if (haggleWindowSub is { IsVisible: true })
            {
                yield return Input.KeyPress(Keys.Escape);
                yield return new WaitTime(Settings.HoverItemDelay * 3);
            }
            yield break;
        }
        yield return ExitAllWindows();

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!labelOnGround.ItemOnGround.Path.Contains("/HagglerHideout"))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                LogError("Tujen not visible");
                yield break;
            }
            Input.SetCursorPos(labelOnGround.Label.GetClientRect().Center);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.KeyDown(Keys.ControlKey);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.Click(MouseButtons.Left);
            Input.KeyUp(Keys.ControlKey);
            yield return new WaitFunctionTimed(() => haggleWindow is { IsVisible: true }, true, 2000, "Tujen not reached in time");
            if (haggleWindow is { IsVisible: false })
            {
                LogError("Tujen not visible");
                yield break;
            }
        }
        yield return true;
    }

    private IEnumerator FindAndClickStash()
    {
        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var stash = GameController.IngameState.IngameUi.StashElement;

        if (stash is { IsVisible: true })
        {
            yield break;
        }

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!labelOnGround.ItemOnGround.Path.Contains("/Stash"))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                LogError("Stash not visible");
                yield break;
            }
            Input.SetCursorPos(labelOnGround.Label.GetClientRect().Center);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.Click(MouseButtons.Left);
            yield return new WaitFunctionTimed(() => stash is { IsVisible: true }, true, 2000, "Stash not reached in time");
            if (stash is { IsVisible: false })
            {
                LogError("Stash not visible");
                yield break;
            }
        }
        yield return true;
    }

    private IEnumerator ExitAllWindows()
    {
        var haggleWindowSub = GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow;
        if (haggleWindowSub is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }

        var haggleWindow = GameController.IngameState.IngameUi.HaggleWindow;
        if (haggleWindow is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }

        var tujenDialog = GameController.IngameState.IngameUi.ExpeditionNpcDialog;
        if (tujenDialog is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }

        var stashWindow = GameController.IngameState.IngameUi.StashElement;
        if (stashWindow is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }

        var inventory = GameController.IngameState.IngameUi.InventoryPanel;
        if (inventory is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }
    }

    private HaggleProcess _process = null;
    private IEnumerator HaggleCoroutine()
    {
        _haggleState = HaggleState.Running;
        yield return FindAndClickTujen();
        var mainWindow = GameController.IngameState.IngameUi.HaggleWindow;

        if (mainWindow is { IsVisible: false })
        {
            DebugWindow.LogError($"Haggle window not open!");
            yield break;
        }

        _process = new HaggleProcess(mainWindow, GameController, NinjaItems, Settings);
        yield return _process.Update();
        while (_process.CanRun() || Settings.DebugOnly)
        {
            _process.InitializeWindow();
            yield return _process.Run();
            if (Settings.DebugOnly)
            {
                break;
            }
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            if (ShouldEmptyInventory())
            {
                yield return EmptyInventoryCoRoutine();
            }
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            if (_process.Stock.Coins > 0)
            {
                var oldCount = GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.TujenRerolls;

                yield return ReRollWindow();

                yield return new WaitFunctionTimed(() => oldCount > GameController.IngameState.IngameUi.HaggleWindow.CurrencyInfo.TujenRerolls, true, 2000, "Could not Refresh");
                if (oldCount == mainWindow.InventoryItems.Count)
                {
                    LogError("Could not Refresh");
                    StopAllRoutines();
                    yield break;
                }
            }
            yield return _process.Update();
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }


        if (!Settings.DebugOnly)
        {
            yield return EmptyInventoryCoRoutine();
        }

        StopAllRoutines();
    }

    private IEnumerator ReRollWindow()
    {
        var win = GameController.IngameState.IngameUi.HaggleWindow;
        if (win is { IsVisible: false })
        {
            DebugWindow.LogError($"Haggle window not open!");
            StopAllRoutines();
            yield break;
        }
        Input.SetCursorPos(win.RefreshItemsButton.GetClientRect().Center);
        yield return new WaitTime(Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(Settings.HoverItemDelay * 3);
    }

    private void StopAllRoutines()
    {
        var routine = Core.ParallelRunner.FindByName(_coroutineName);
        routine?.Done();
        routine = Core.ParallelRunner.FindByName(_empty_inventory_coroutine_name);
        routine?.Done();
        routine = Core.ParallelRunner.FindByName(_reroll_coroutine_name);
        routine?.Done();
        routine = Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless);
        routine?.Done();
        routine = Core.ParallelRunner.FindByName(BuyAssistance.BuyAssistance._sendPmCoroutineName);
        routine?.Done();
        routine = Core.ParallelRunner.FindByName(BuyAssistance.BuyAssistance._extractMoneyFromStashCoroutineName);
        routine?.Done();
        _haggleState = HaggleState.Idle;
        Input.KeyUp(Keys.ControlKey);
    }

    public override void Render()
    {
        if (Settings.ShowDebugWindow && (_haggleState is HaggleState.Running || Settings.DebugOnly))
        {
            var show = Settings.ShowDebugWindow.Value;
            // Set next window size
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(800, 300));
            ImGui.Begin("Debug Mode Window", ref show);
            Settings.ShowDebugWindow.Value = show;

            if (ImGui.BeginTable("table1", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                // Header Row
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Type");
                ImGui.TableSetupColumn("Amount");
                ImGui.TableSetupColumn("Value");
                ImGui.TableSetupColumn("Price");
                ImGui.TableSetupColumn("State");
                ImGui.TableHeadersRow();

                if (_process != null && _process.CurrentWindow != null)
                {
                    try
                    {
                        foreach (HaggleItem haggleItem in _process.CurrentWindow.Items)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Name);
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Type);
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Amount.ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Value.ToString(CultureInfo.InvariantCulture) + "c");
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Price?.TotalValue().ToString(CultureInfo.InvariantCulture) + "c");
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.State.ToString());
                        }
                    }
                    catch
                    {
                    }
                }


                ImGui.EndTable();
            }

            ImGui.End();
        }

        var expeditionWindow = GameController.IngameState.IngameUi.ExpeditionWindow;
        if (Settings.ExpeditionMapHelper && expeditionWindow is { IsVisible: true })
        {
            var _factionOrder = Settings.PrepareLogbookSettings.FactionOrder.Value.Split(',').ToArray();
            var _areaOrder = Settings.PrepareLogbookSettings.AreaOrder.Value.Split(',').ToArray();

            var expeditionMap = expeditionWindow.GetChildAtIndex(0).GetChildAtIndex(0).GetChildAtIndex(0);
            var expeditionNodes = expeditionMap.Children.Select(ExpeditionNode.FromElement).ToList();
            if (expeditionNodes.Count > 0)
            {
                expeditionNodes.Sort((node1, node2) =>
                {
                    int index1Array1 = Array.IndexOf(_factionOrder, node1.Faction);
                    int index2Array1 = Array.IndexOf(_factionOrder, node2.Faction);
                    int index1Array2 = Array.IndexOf(_areaOrder, node1.Area);
                    int index2Array2 = Array.IndexOf(_areaOrder, node2.Area);

                    // If key isn't found in array, treat its index as int.MaxValue (i.e., put it at the end)
                    index1Array1 = index1Array1 == -1 ? int.MaxValue : index1Array1;
                    index2Array1 = index2Array1 == -1 ? int.MaxValue : index2Array1;

                    int comparison = index1Array1.CompareTo(index2Array1);
                    if (comparison == 0)
                    {
                        // Only sort by array2's Key2 if Key1s were equal
                        return index1Array2.CompareTo(index2Array2);
                    }

                    return comparison;
                });

                var chosenNode = expeditionNodes.First();
                Graphics.DrawFrame(chosenNode.Position.TopLeft, chosenNode.Position.BottomRight, Color.Orange, 3);
            }
        }

        if (Settings.EnableBuyAssistance)
            BuyAssistance.BuyAssistance.Render();


        if (GameController.IngameState.IngameUi.HaggleWindow.IsVisible)
        {
            if (
                !Settings.ArtifactValueSettings.EnableLesser ||
                !Settings.ArtifactValueSettings.EnableGreater ||
                !Settings.ArtifactValueSettings.EnableGrand ||
                !Settings.ArtifactValueSettings.EnableExceptional
            )
            {
                var rect = GameController.IngameState.IngameUi.HaggleWindow.GetChildAtIndex(3).GetClientRect();
                var textPos = new Vector2(rect.X, rect.Y - 20);
                Graphics.DrawText("WARNING: Some artifacts are disabled", textPos, Color.Red, 20);
            }
        }

        if (Settings.EnableVoranaWarning && _areaHasVorana)
        {
            if (_areaHasChangedVoranaSoundPlayer == null)
            {
                var soundPath = Path.Combine(DirectoryFullName, "sounds\\vorana.wav");
                _areaHasChangedVoranaSoundPlayer = new SoundPlayer(soundPath);
            }
            if (Settings.EnableVoranaWarningSound)
            {
                if (_areaHasVoranaJumps == 0)
                {
                    _areaHasChangedVoranaSoundPlayer.Play();
                }
                if (_areaHasVoranaSoundClipJumps == 0)
                    _areaHasVoranaSoundClipJumps++;
            }


            _areaHasVoranaJumps++;

            var screenWidth = GameController.Window.GetWindowRectangle().Width;
            var screenHeight = GameController.Window.GetWindowRectangle().Height;

            // measure text and draw in the middle 
            var txt = "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";
            txt += txt;
            txt += txt;
            var textSize = Graphics.MeasureText(txt, 20);

            if (_areaHasVoranaJumps % 10 == 0)
            {
                _areaHasChangedVoranaColor = _areaHasChangedVoranaColor == Color.Red ? Color.Yellow : Color.Red;
            }


            var drawNum = (screenHeight - (int)(screenHeight * 0.2)) / textSize.Y;
            for (int i = 0; i < drawNum; i++)
            {
                var y = screenHeight / drawNum * i;
                Graphics.DrawText(txt, new Vector2(screenWidth / 2 - textSize.X / 2, y), _areaHasChangedVoranaColor, 20);
            }

            if (_areaHasVoranaJumps > 100)
            {
                _areaHasVorana = false;
                _areaHasVoranaJumps = 0;
            }
        }
        if (Settings.EnableVoranaWarningSound)
        {
            if (_areaHasVoranaSoundClipJumps > 0)
            {
                _areaHasVoranaSoundClipJumps++;
            }
            if (_areaHasVoranaSoundClipJumps > 500)
            {
                _areaHasVoranaSoundClipJumps = 0;
                _areaHasChangedVoranaSoundPlayer.Stop();
            }
        }

        return;
    }
    private int _areaHasVoranaJumps = 0;
    private int _areaHasVoranaSoundClipJumps = 0;
    private Color _areaHasChangedVoranaColor = Color.Red;
    private SoundPlayer _areaHasChangedVoranaSoundPlayer = null;


    public override void EntityAdded(Entity entity)
    {
        if (entity.Path.Contains("Vorana"))
        {
            _areaHasVorana = true;
        }
    }
}