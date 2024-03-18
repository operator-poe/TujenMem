﻿using SharpDX;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using System.Collections;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Globalization;
using ImGuiNET;
using ExileCore.PoEMemory.Elements;
using System.IO;
using System.Media;

namespace TujenMem;

public class TujenMem : BaseSettingsPlugin<TujenMemSettings>
{
    internal static TujenMem Instance;

    public HaggleState HaggleState = HaggleState.Idle;

    private bool _areaHasVorana = false;

    public override bool Initialise()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        Settings.SetDefaults();
        Settings.PrepareLogbookSettings.SetDefaults();

        Input.RegisterKey(Settings.HotKeySettings.StartHotKey);
        Settings.HotKeySettings.StartHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.StartHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.StopHotKey);
        Settings.HotKeySettings.StopHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.StopHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey);
        Settings.HotKeySettings.RollAndBlessHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey); };

        Ninja.CheckIntegrity();
        Task.Run(Ninja.Parse);

        return base.Initialise();
    }

    public override void AreaChange(AreaInstance area)
    {
        _areaHasVorana = false;
    }

    private readonly static string _coroutineName = "TujenMem_Haggle";
    private readonly static string _empty_inventory_coroutine_name = "TujenMem_Inventory";
    private readonly static string _reroll_coroutine_name = "TujenMem_Reroll";
    public override Job Tick()
    {
        if (Settings.HotKeySettings.StartHotKey.PressedOnce())
        {
            Log.Debug("Start Hotkey pressed");
            if (Core.ParallelRunner.FindByName(_coroutineName) == null)
            {
                Log.Debug("Starting Haggle Coroutine");
                HaggleState = HaggleState.StartUp;
                Core.ParallelRunner.Run(new Coroutine(HaggleCoroutine(), this, _coroutineName));
            }
            else
            {
                Log.Debug("Stopping Haggle Coroutine");
                HaggleState = HaggleState.Cancelling;
            }
        }
        if (Settings.HotKeySettings.RollAndBlessHotKey.PressedOnce())
        {
            Log.Debug("Roll and Bless Hotkey pressed");
            if (Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless) == null)
            {
                Log.Debug("Starting Roll and Bless Coroutine");
                Core.ParallelRunner.Run(new Coroutine(PrepareLogbook.Runner.RollAndBlessLogbooksCoroutine(), this, PrepareLogbook.Runner.CoroutineNameRollAndBless));
            }
            else
            {
                Log.Debug("Stopping Roll and Bless Coroutine");
                var routine = Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless);
                routine?.Done();
            }
        }
        if (HaggleState is HaggleState.Cancelling)
        {
            Log.Debug("Cancelling Haggle Coroutine");
            StopAllRoutines();
            HaggleState = HaggleState.Idle;
        }
        if (Settings.HotKeySettings.StopHotKey.PressedOnce())
        {
            Log.Debug("Stop Hotkey pressed");
            StopAllRoutines();
        }

        if (Settings.SillyOrExperimenalFeatures.EnableBuyAssistance)
            BuyAssistance.BuyAssistance.Tick();


        return null;
    }

    public bool ShouldEmptyInventory()
    {
        Log.Debug("Checking if should empty inventory");
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
                Log.Debug("Should empty inventory");
                return true;
            }
        }
        Log.Debug("Should not empty inventory");
        return false;
    }

    public IEnumerator EmptyInventoryCoRoutine()
    {
        Log.Debug("Emptying Inventory");
        yield return ExitAllWindows();
        yield return FindAndClickStash();

        var inventory = GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
        var inventoryItems = inventory.InventorySlotItems;

        inventoryItems = inventoryItems.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();

        Log.Debug($"Found {inventoryItems.Count} items in inventory");

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

        Log.Debug("Inventory emptied");

        yield return FindAndClickTujen();

        yield return new WaitTime(Settings.HoverItemDelay * 3);
    }

    private IEnumerator FindAndClickTujen()
    {
        Log.Debug("Finding and clicking Tujen");
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
                Error.AddAndShow("Error", "Tujen not visible.\nMake sure that he is positioned within short reach.");
                yield break;
            }
            Input.SetCursorPos(labelOnGround.Label.GetClientRect().Center);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.KeyDown(Keys.ControlKey);
            yield return new WaitTime(Settings.HoverItemDelay);
            Input.Click(MouseButtons.Left);
            Input.KeyUp(Keys.ControlKey);
            yield return new WaitFunctionTimed(() => haggleWindow is { IsVisible: true }, false, 1000);
            if (haggleWindow is { IsVisible: false })
            {
                Error.AddAndShow("Error", "Could not reach Tujen in time.\nMake sure that he is positioned within short reach.");
                yield break;
            }
        }
        Log.Debug("Found and clicked Tujen");
        yield return true;
    }

    private IEnumerator FindAndClickStash()
    {
        Log.Debug("Finding and clicking Stash");
        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var stash = GameController.IngameState.IngameUi.StashElement;

        if (stash is { IsVisible: true })
        {
            Log.Debug("Stash already open");
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
            yield return new WaitFunctionTimed(() => stash is { IsVisible: true }, false, 1000);
            if (stash is { IsVisible: false })
            {
                Error.AddAndShow("Error", "Could not reach Stash in time.\nMake sure that it is positioned within short reach.");
                yield break;
            }
        }
        Log.Debug("Found and clicked Stash");
        yield return true;
    }

    private IEnumerator ExitAllWindows()
    {
        Log.Debug("Exiting all windows");
        var haggleWindowSub = GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow;
        if (haggleWindowSub is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            Log.Debug("Exited Haggle Sub window");
        }

        var haggleWindow = GameController.IngameState.IngameUi.HaggleWindow;
        if (haggleWindow is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            Log.Debug("Exited Haggle window");
        }

        var tujenDialog = GameController.IngameState.IngameUi.ExpeditionNpcDialog;
        if (tujenDialog is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            Log.Debug("Exited Tujen dialog");
        }

        var stashWindow = GameController.IngameState.IngameUi.StashElement;
        if (stashWindow is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            Log.Debug("Exited Stash window");
        }

        var inventory = GameController.IngameState.IngameUi.InventoryPanel;
        if (inventory is { IsVisible: true })
        {
            yield return Input.KeyPress(Keys.Escape);
            yield return new WaitTime(Settings.HoverItemDelay * 3);
            Log.Debug("Exited Inventory window");
        }
    }

    private bool StartUpChecks()
    {
        Error.Clear();
        if (!Ninja.IsValid || Ninja.Items.Count == 0)
        {
            Error.Add("Startup error", "Ninja prices could not be loaded.\nPlease check the settings and make sure all files are downloaded and valid.");
        }
        // check haggle stock
        if (HaggleStock.Coins == 0)
        {
            Error.Add("Startup error", "No coins found in Haggle window.\nPlease make sure you have coins.\n(Check HUD log for errors)");
        }
        if (HaggleStock.Lesser == 0 && Settings.ArtifactValueSettings.EnableLesser)
        {
            Error.Add("Startup error", "No Lesser artifacts found in Haggle window.\nPlease make sure you have Lesser artifacts.\n(Check HUD log for errors)");
        }
        if (HaggleStock.Greater == 0 && Settings.ArtifactValueSettings.EnableGreater)
        {
            Error.Add("Startup error", "No Greater artifacts found in Haggle window.\nPlease make sure you have Greater artifacts.\n(Check HUD log for errors)");
        }
        if (HaggleStock.Grand == 0 && Settings.ArtifactValueSettings.EnableGrand)
        {
            Error.Add("Startup error", "No Grand artifacts found in Haggle window.\nPlease make sure you have Grand artifacts.\n(Check HUD log for errors)");
        }
        if (HaggleStock.Exceptional == 0 && Settings.ArtifactValueSettings.EnableExceptional)
        {
            Error.Add("Startup error", "No Exceptional artifacts found in Haggle window.\nPlease make sure you have Exceptional artifacts.\n(Check HUD log for errors)");
        }

        Error.ShowIfNeeded();
        return Error.IsDisplaying;
    }

    private HaggleProcess _process = null;
    private IEnumerator HaggleCoroutine()
    {
        HaggleState = HaggleState.Running;
        Log.Debug("Starting Haggle process");
        yield return FindAndClickTujen();
        var mainWindow = GameController.IngameState.IngameUi.HaggleWindow;

        if (mainWindow is { IsVisible: false })
        {
            Log.Error("Haggle window not open!");
            yield break;
        }

        if (StartUpChecks())
        {
            yield break;
        }

        Log.Debug("Initiaizing Haggle process");
        _process = new HaggleProcess();
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
            if (HaggleStock.Coins > 0)
            {
                var oldCount = HaggleStock.Coins;

                yield return ReRollWindow();

                yield return new WaitFunctionTimed(() => oldCount > HaggleStock.Coins, false, 500);
                if (oldCount == HaggleStock.Coins)
                {
                    Error.AddAndShow("Error", "Window did not reroll after attempting a click.\nCheck your hover delay and make sure that the window is not obstructed.");
                    yield break;
                }
            }
            yield return new WaitTime(Settings.HoverItemDelay * 3);
        }


        if (!Settings.DebugOnly && Settings.EmptyInventoryAfterHaggling)
        {
            yield return EmptyInventoryCoRoutine();
        }

        StopAllRoutines();
    }

    private IEnumerator ReRollWindow()
    {
        Log.Debug("ReRolling window");
        if (GameController.IngameState.IngameUi.HaggleWindow is { IsVisible: false })
        {
            Error.AddAndShow("Error while ReRolling", "Haggle window not open!");
            yield break;
        }
        Input.SetCursorPos(GameController.IngameState.IngameUi.HaggleWindow.RefreshItemsButton.GetClientRect().Center);
        yield return new WaitTime(Settings.HoverItemDelay);
        Input.Click(MouseButtons.Left);
        yield return new WaitTime(Settings.HoverItemDelay * 3);
        Log.Debug("ReRolled window");
    }

    public void StopAllRoutines()
    {
        Log.Debug("Stopping all routines");
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
        HaggleState = HaggleState.Idle;
        Input.KeyUp(Keys.ControlKey);
    }

    public bool IsAnyRoutineRunning
    {
        get
        {
            return Core.ParallelRunner.FindByName(_coroutineName) != null
                || Core.ParallelRunner.FindByName(_empty_inventory_coroutine_name) != null
                || Core.ParallelRunner.FindByName(_reroll_coroutine_name) != null
                || Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless) != null
                || Core.ParallelRunner.FindByName(BuyAssistance.BuyAssistance._sendPmCoroutineName) != null
                || Core.ParallelRunner.FindByName(BuyAssistance.BuyAssistance._extractMoneyFromStashCoroutineName) != null;
        }
    }

    public override void Render()
    {
        Error.Render();

        if (Settings.ShowDebugWindow && (HaggleState is HaggleState.Running || Settings.DebugOnly))
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
            var _factionOrder = Settings.PrepareLogbookSettings.FactionOrder;
            var _areaOrder = Settings.PrepareLogbookSettings.AreaOrder;

            var expeditionMap = expeditionWindow.GetChildAtIndex(0).GetChildAtIndex(0).GetChildAtIndex(0);
            var expeditionNodes = expeditionMap.Children.Select(ExpeditionNode.FromElement).ToList();
            if (expeditionNodes.Count > 0)
            {
                expeditionNodes.Sort((node1, node2) =>
                {
                    int index1Array1 = _factionOrder.IndexOf(node1.Faction);
                    int index2Array1 = _factionOrder.IndexOf(node2.Faction);
                    int index1Array2 = _areaOrder.IndexOf(node1.Area);
                    int index2Array2 = _areaOrder.IndexOf(node2.Area);

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

        if (Settings.SillyOrExperimenalFeatures.EnableBuyAssistance)
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

        if (Settings.SillyOrExperimenalFeatures.EnableVoranaWarning && _areaHasVorana)
        {
            if (_areaHasChangedVoranaSoundPlayer == null)
            {
                var soundPath = Path.Combine(DirectoryFullName, "sounds\\vorana.wav");
                _areaHasChangedVoranaSoundPlayer = new SoundPlayer(soundPath);
            }
            if (Settings.SillyOrExperimenalFeatures.EnableVoranaWarningSound)
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
        if (Settings.SillyOrExperimenalFeatures.EnableVoranaWarningSound)
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