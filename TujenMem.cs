using SharpDX;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
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
using System;

namespace TujenMem;

public class TujenMem : BaseSettingsPlugin<TujenMemSettings>
{
    internal static TujenMem Instance;
    public Scheduler Scheduler = new();

    public HaggleState HaggleState = HaggleState.Idle;

    private bool _areaHasVorana = false;
    private bool _stopAfterCurrentWindow = false;
    private bool _wasStoppedAfterCurrentWindow = false;

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
        Input.RegisterKey(Settings.HotKeySettings.StopAfterCurrentWindowHotKey);
        Settings.HotKeySettings.StopAfterCurrentWindowHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.StopAfterCurrentWindowHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey);
        Settings.HotKeySettings.RollAndBlessHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.RollAndBlessHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.IdentifyHotKey);
        Settings.HotKeySettings.IdentifyHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.IdentifyHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.InventoryHotKey);
        Settings.HotKeySettings.InventoryHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.InventoryHotKey); };
        Input.RegisterKey(Settings.HotKeySettings.TestHotKey);
        Settings.HotKeySettings.TestHotKey.OnValueChanged += () => { Input.RegisterKey(Settings.HotKeySettings.TestHotKey); };

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

    private async SyncTask<bool> TestCoroutine()
    {
        Log.Debug("Test Coroutine - Testing Snapshot System");

        // Test snapshot creation and diff calculation
        var snapshot1 = Stash.Inventory.CreateSnapshot();
        Log.Debug($"Snapshot 1 created with {snapshot1.GetAllItems().Count} items");

        // Wait a bit to simulate some time passing
        await InputAsync.WaitX(2);

        var snapshot2 = Stash.Inventory.CreateSnapshot();
        Log.Debug($"Snapshot 2 created with {snapshot2.GetAllItems().Count} items");

        var diff = Stash.Inventory.CalculateDiff(snapshot1, snapshot2);
        Log.Debug($"Diff between snapshots: {diff.Count} new items");

        // Test baseline functionality
        Log.Debug($"Baseline debug info: {Stash.Inventory.GetSnapshotDebugInfo()}");

        // Test the stash item functionality
        await Stash.Utils.StashItemTypeToTab("4", "JewelAbyss");

        return true;
    }

    public override Job Tick()
    {
        Scheduler.Run();

        if (Settings.HotKeySettings.TestHotKey.PressedOnce())
        {
            Log.Debug("Test Hotkey pressed");
            if (Core.ParallelRunner.FindByName("_TEST_") == null)
            {
                Scheduler.AddTask(TestCoroutine(), "_TEST_");
            }
        }

        if (Settings.HotKeySettings.StartHotKey.PressedOnce() || Input.IsKeyDown(Settings.HotKeySettings.StartHotKey.Value))
        {
            Log.Debug("Start Hotkey pressed");
            if (Core.ParallelRunner.FindByName(_coroutineName) == null)
            {
                Log.Debug("Starting Haggle Coroutine");
                HaggleState = HaggleState.StartUp;
                Scheduler.AddRestartableTask(
                    () => HaggleCoroutine(),
                    "HaggleCoroutine",
                    () => StopAllRoutines(skipSchedulerStop: true),
                    maxRetries: 3
                );
                // Scheduler.AddTask(HaggleCoroutine(), "HaggleCoroutine");
            }
            else
            {
                Log.Debug("Stopping Haggle Coroutine");
                HaggleState = HaggleState.Cancelling;
            }
        }
        if (Settings.HotKeySettings.RollAndBlessHotKey.PressedOnce() || Input.IsKeyDown(Settings.HotKeySettings.RollAndBlessHotKey.Value))
        {
            Log.Debug("Roll and Bless Hotkey pressed");
            if (Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless) == null)
            {
                Log.Debug("Starting Roll and Bless Coroutine");
                Scheduler.AddTask(PrepareLogbook.Runner.RollAndBlessLogbooksCoroutine(), PrepareLogbook.Runner.CoroutineNameRollAndBless);
            }
            else
            {
                Log.Debug("Stopping Roll and Bless Coroutine");
                HaggleState = HaggleState.Cancelling;
            }
        }
        if (Settings.HotKeySettings.IdentifyHotKey.PressedOnce() || Input.IsKeyDown(Settings.HotKeySettings.IdentifyHotKey.Value))
        {
            Log.Debug("Identify Items Hotkey Pressed");
            if (Core.ParallelRunner.FindByName(PrepareLogbook.Runner.CoroutineNameRollAndBless + "_Identify") == null)
            {
                Log.Debug("Starting Identify Items Coroutine");
                Scheduler.AddTask(PrepareLogbook.Runner.IdentifyItemsInStash(), PrepareLogbook.Runner.CoroutineNameRollAndBless + "_Identify");
            }
            else
            {
                Log.Debug("Stopping Roll and Bless Coroutine");
                HaggleState = HaggleState.Cancelling;
            }
        }
        if (HaggleState is HaggleState.Cancelling)
        {
            Log.Debug("Cancelling Haggle Coroutine");
            StopAllRoutines();
            HaggleState = HaggleState.Idle;
        }
        if (Settings.HotKeySettings.StopHotKey.PressedOnce() || Input.IsKeyDown(Settings.HotKeySettings.StopHotKey.Value))
        {
            Log.Debug("Stop Hotkey pressed");
            StopAllRoutines();
        }

        if (Settings.SillyOrExperimenalFeatures.EnableBuyAssistance)
            BuyAssistance.BuyAssistance.Tick();

        if (Settings.HotKeySettings.StopAfterCurrentWindowHotKey.PressedOnce())
        {
            if (!_stopAfterCurrentWindow)
            {
                Log.Debug("StopAfterCurrentWindowHotKey pressed - will stop after current window");
                _stopAfterCurrentWindow = true;
            }
        }
        // Show debug info if stop after current window is active
        if (_stopAfterCurrentWindow && HaggleState == HaggleState.Running)
        {
            Log.Debug("Stop after current window is active - will stop after next reroll");
        }

        return null;
    }

    public bool ShouldEmptyInventory()
    {
        Log.Debug("Checking if should empty inventory");

        // Use the snapshot system to check if there are items in the trigger row (row 10)
        var snapshot = Stash.Inventory.CreateSnapshot();

        for (int y = 0; y < 5; y++)
        {
            if (snapshot.IsOccupied(10, y))
            {
                Log.Debug("Should empty inventory - found item in trigger row");
                return true;
            }
        }

        Log.Debug("Should not empty inventory");
        return false;
    }

    public async SyncTask<bool> EmptyInventoryCoRoutine()
    {
        Log.Debug("Emptying Inventory");

        await ExitAllWindows();
        await FindAndClickStash();

        // Stash items based on type mappings
        foreach (var mapping in Settings.StashSettings.StashTabMappings)
        {
            await Stash.Utils.StashItemTypeToTab(mapping.Item2, mapping.Item1);
        }

        // Select the primary tab after stashing special items
        if (!string.IsNullOrWhiteSpace(Settings.StashSettings.PrimaryStashTab.Value))
        {
            await Stash.Stash.SelectTab(Settings.StashSettings.PrimaryStashTab.Value);
        }
        else
        {
            // Fallback to first tab if primary is not set
            await Stash.Stash.SelectTab(0);
        }

        // Get items that were added since the baseline (new items from haggling)
        var newItems = Stash.Inventory.CalculateDiffFromBaseline();

        // Also include any items that were already in the inventory (excluding the reserved rows)
        var existingItems = Stash.Inventory.GetItemsToStash(10, 11);

        // Combine and remove duplicates
        var itemsToStash = newItems.Concat(existingItems).DistinctBy(x => x.PosX * 1000 + x.PosY).OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();

        Log.Debug($"Found {itemsToStash.Count} items to stash ({newItems.Count} new, {existingItems.Count} existing)");

        if (itemsToStash.Count > 0)
        {
            await InputAsync.KeyDown(Keys.ControlKey);
            foreach (var item in itemsToStash)
            {
                if (item?.Item != null)
                {
                    await InputAsync.ClickElement(item.GetClientRect());
                }
            }
            await InputAsync.KeyUp(Keys.ControlKey);
        }

        Log.Debug("Inventory emptied");

        await FindAndClickTujen();
        await InputAsync.Wait();
        await InputAsync.Wait();
        await InputAsync.Wait();
        return true;
    }

    private async SyncTask<bool> FindAndClickTujen()
    {
        Log.Debug("Finding and clicking Tujen");
        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var haggleWindow = GameController.IngameState.IngameUi.HaggleWindow;

        if (haggleWindow is { IsVisible: true })
        {
            var haggleWindowSub = GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow;
            if (haggleWindowSub is { IsVisible: true })
            {
                await InputAsync.KeyPress(Keys.Escape);
                await InputAsync.Wait();
                await InputAsync.Wait();
                await InputAsync.Wait();
            }
            return true;
        }
        await ExitAllWindows();

        foreach (LabelOnGround labelOnGround in itemsOnGround)
        {
            if (!labelOnGround.ItemOnGround.Path.Contains("/HagglerHideout"))
            {
                continue;
            }
            if (!labelOnGround.IsVisible)
            {
                Error.AddAndShow("Error", "Tujen not visible.\nMake sure that he is positioned within short reach.");
                return false;
            }
            await InputAsync.KeyDown(Keys.ControlKey);
            await InputAsync.Wait();
            // await InputAsync.ClickElement(labelOnGround.Label.GetClientRect().Center);
            await InputAsync.ClickElement(labelOnGround.Label.GetClientRect());
            await InputAsync.KeyUp(Keys.ControlKey);
            await InputAsync.Wait(() => haggleWindow is { IsVisible: true }, 1000);
            if (haggleWindow is { IsVisible: false })
            {
                Error.AddAndShow("Error", "Could not reach Tujen in time.\nMake sure that he is positioned within short reach.");
                return false;
            }
        }
        Log.Debug("Found and clicked Tujen");
        return true;
    }

    private async SyncTask<bool> FindAndClickStash()
    {
        Log.Debug("Finding and clicking Stash");
        var itemsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabels;
        var stash = GameController.IngameState.IngameUi.StashElement;

        if (stash is { IsVisible: true })
        {
            Log.Debug("Stash already open");
            return true;
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
                return false;
            }
            // await InputAsync.ClickElement(labelOnGround.Label.GetClientRect().Center);
            await InputAsync.ClickElement(labelOnGround.Label.GetClientRect());
            await InputAsync.Wait(() => stash is { IsVisible: true }, 1000);
            if (stash is { IsVisible: false })
            {
                Error.AddAndShow("Error", "Could not reach Stash in time.\nMake sure that it is positioned within short reach.");
                return false;
            }
        }
        Log.Debug("Found and clicked Stash");
        return true;
    }

    private async SyncTask<bool> ExitAllWindows()
    {
        Log.Debug("Exiting all windows");
        var haggleWindowSub = GameController.IngameState.IngameUi.HaggleWindow.TujenHaggleWindow;
        if (haggleWindowSub is { IsVisible: true })
        {
            await InputAsync.KeyPress(Keys.Escape);
            await InputAsync.Wait();
            await InputAsync.Wait();
            await InputAsync.Wait();
            Log.Debug("Exited Haggle Sub window");
        }

        var haggleWindow = GameController.IngameState.IngameUi.HaggleWindow;
        if (haggleWindow is { IsVisible: true })
        {
            await InputAsync.KeyPress(Keys.Escape);
            await InputAsync.Wait();
            await InputAsync.Wait();
            await InputAsync.Wait();
            Log.Debug("Exited Haggle window");
        }

        var tujenDialog = GameController.IngameState.IngameUi.ExpeditionNpcDialog;
        if (tujenDialog is { IsVisible: true })
        {
            await InputAsync.KeyPress(Keys.Escape);
            await InputAsync.Wait();
            await InputAsync.Wait();
            await InputAsync.Wait();
            Log.Debug("Exited Tujen dialog");
        }

        var stashWindow = GameController.IngameState.IngameUi.StashElement;
        if (stashWindow is { IsVisible: true })
        {
            await InputAsync.KeyPress(Keys.Escape);
            await InputAsync.Wait();
            await InputAsync.Wait();
            await InputAsync.Wait();
            Log.Debug("Exited Stash window");
        }

        var inventory = GameController.IngameState.IngameUi.InventoryPanel;
        if (inventory is { IsVisible: true })
        {
            await InputAsync.KeyPress(Keys.Escape);
            await InputAsync.Wait();
            await InputAsync.Wait();
            await InputAsync.Wait();
            Log.Debug("Exited Inventory window");
        }

        return true;
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
    private async SyncTask<bool> HaggleCoroutine()
    {
        // Reset the stop after current window flag
        _stopAfterCurrentWindow = false;

        // Interject here for Gwennen
        if (Settings.Gwennen.EnableGwennen && GwennenRunner.CanGwennen())
        {
            return await GwennenRunner.Run();
        }

        HaggleState = HaggleState.Running;
        Log.Debug("Starting Haggle process");
        await FindAndClickTujen();
        var mainWindow = GameController.IngameState.IngameUi.HaggleWindow;

        if (mainWindow is { IsVisible: false })
        {
            Log.Error("Haggle window not open!");
            return false;
        }

        if (StartUpChecks())
        {
            Log.Error("Startup checks failed!");
            return false;
        }

        // Create baseline snapshot for inventory tracking when haggle process starts
        Log.Debug("Creating baseline inventory snapshot");
        Stash.Inventory.CreateBaselineSnapshot();

        Log.Debug("Initiaizing Haggle process");
        _process = new HaggleProcess();
        while (_process.CanRun() || Settings.DebugOnly)
        {
            _process.InitializeWindow();

            // Check if item count is below 10 and instantly reroll if so
            var itemCount = GameController.IngameState.IngameUi.HaggleWindow.InventoryItems.Count;
            Log.Debug($"Current item count: {itemCount}");

            if (itemCount < 10)
            {
                Log.Debug("Item count below 10, instantly rerolling window");
                await ReRollWindow();
                await InputAsync.WaitX(3);
                continue;
            }

            await _process.Run();
            if (Settings.DebugOnly)
            {
                break;
            }
            await InputAsync.Wait();
            if (ShouldEmptyInventory())
            {
                await EmptyInventoryCoRoutine();
            }
            await InputAsync.WaitX(3);
            if (HaggleStock.Coins > 0)
            {
                var oldCount = HaggleStock.Coins;

                await ReRollWindow();

                await InputAsync.Wait(() => oldCount > HaggleStock.Coins, 500);
                if (oldCount == HaggleStock.Coins)
                {
                    Error.AddAndShow("Error", "Window did not reroll after attempting a click.\nCheck your hover delay and make sure that the window is not obstructed.");
                    StopAllRoutines();
                    return false;
                }

                // Check if user wants to stop after current window
                if (_stopAfterCurrentWindow)
                {
                    Log.Debug("Stopping after current window as requested");
                    _stopAfterCurrentWindow = false;
                    _wasStoppedAfterCurrentWindow = true;
                    break;
                }
            }
            await InputAsync.Wait();
        }

        // After the main loop, handle inventory emptying based on how the coroutine was stopped
        if (!Settings.DebugOnly)
        {
            if (_wasStoppedAfterCurrentWindow)
            {
                if (Settings.EmptyInventoryOnStopAfterCurrentWindow)
                {
                    await EmptyInventoryCoRoutine();
                }
                else
                {
                    Log.Debug("Skipping inventory emptying due to setting");
                }
            }
            else if (Settings.EmptyInventoryAfterHaggling)
            {
                await EmptyInventoryCoRoutine();
            }
        }

        StopAllRoutines();

        return true;
    }

    public async SyncTask<bool> ReRollWindow()
    {
        Log.Debug("ReRolling window");
        if (GameController.IngameState.IngameUi.HaggleWindow is { IsVisible: false })
        {
            Error.AddAndShow("Error while ReRolling", "Haggle window not open!");
            return false;
        }
        await InputAsync.ClickElement(GameController.IngameState.IngameUi.HaggleWindow.RefreshItemsButton.GetClientRect());
        await InputAsync.Wait();
        await InputAsync.Wait();
        Log.Debug("ReRolled window");
        return true;
    }

    public void StopAllRoutines(bool skipSchedulerStop = false)
    {
        Log.Debug("Stopping all routines");
        if (!skipSchedulerStop)
        {
            Scheduler.Stop();
        }
        Scheduler.Clear();
        InputAsync.LOCK_CONTROLLER = false;
        InputAsync.IControllerEnd();
        HaggleState = HaggleState.Idle;
        _stopAfterCurrentWindow = false;
        _wasStoppedAfterCurrentWindow = false;
        if (_process != null && _process.CurrentWindow != null)
        {
            _process.CurrentWindow.ClearItems();
        }
        Input.KeyUp(Keys.ControlKey);
        Input.KeyUp(Keys.ShiftKey);
        HaggleStock.StockType = StockType.Tujen;
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

        var drawList = ImGui.GetBackgroundDrawList();

        StatisticsUI.Render(Settings.SillyOrExperimenalFeatures.ShowStatisticsWindow);

        if (Settings.ShowDebugWindow && (HaggleState is HaggleState.Running || Settings.DebugOnly) && GameController.IngameState.IngameUi.HaggleWindow is { IsVisible: true })
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
                        int currentRow = 0;
                        float targetScrollY = 0;
                        float rowHeight = ImGui.GetTextLineHeightWithSpacing();
                        float windowHeight = ImGui.GetWindowHeight();
                        float scrollMargin = rowHeight * 2; // 2 rows worth of margin

                        foreach (HaggleItem haggleItem in _process.CurrentWindow.Items)
                        {
                            ImGui.TableNextRow();
                            if (haggleItem == _process.CurrentWindow.CurrentHagglingItem)
                            {
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(1, 0.5f, 0, 0.3f)));
                                // Calculate target scroll position with margin
                                targetScrollY = currentRow * rowHeight - scrollMargin;
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Name);
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Type);
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.Amount.ToString());
                            ImGui.TableNextColumn();
                            if (haggleItem.ActualValue > 0)
                            {
                                ImGui.Text(Math.Round(haggleItem.ActualValue, 2).ToString(CultureInfo.InvariantCulture) + "c");
                            }
                            else
                            {
                                ImGui.Text(Math.Round(haggleItem.Value, 2).ToString(CultureInfo.InvariantCulture) + "c");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text(Math.Round(haggleItem.Price?.TotalValue() ?? 0, 2).ToString(CultureInfo.InvariantCulture) + "c");
                            ImGui.TableNextColumn();
                            ImGui.Text(haggleItem.State.ToString());
                            currentRow++;
                        }

                        // Apply smooth scrolling if we have a target
                        if (targetScrollY > 0)
                        {
                            float currentScrollY = ImGui.GetScrollY();
                            float maxScrollY = ImGui.GetScrollMaxY();
                            float targetY = Math.Min(targetScrollY, maxScrollY);

                            // Smooth scroll interpolation
                            float newScrollY = currentScrollY + (targetY - currentScrollY) * 0.1f;
                            ImGui.SetScrollY(newScrollY);
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

        // Render floating values on items in haggle window
        if (GameController.IngameState.IngameUi.HaggleWindow is { IsVisible: true } && _process?.CurrentWindow?.Items != null)
        {
            foreach (HaggleItem haggleItem in _process.CurrentWindow.Items)
            {
                // Show floating values for all states except None
                if (haggleItem.State != HaggleItemState.None)
                {
                    var pos = haggleItem.Position.TopLeft;
                    var size = haggleItem.Position.Size;

                    // Choose color based on state (for value text and border)
                    uint borderColor;
                    uint valueTextColor;

                    switch (haggleItem.State)
                    {
                        case HaggleItemState.Priced:
                            borderColor = 0xFF00FF00; // Green (AABBGGRR)
                            valueTextColor = 0xFF00FF00;
                            break;
                        case HaggleItemState.TooExpensive:
                            borderColor = 0xFF0000FF; // Red
                            valueTextColor = 0xFF0000FF;
                            break;
                        case HaggleItemState.Rejected:
                            borderColor = 0xFF808080; // Gray
                            valueTextColor = 0xFF808080;
                            break;
                        case HaggleItemState.Bought:
                            borderColor = 0xFFFF0000; // Blue
                            valueTextColor = 0xFFFF0000;
                            break;
                        case HaggleItemState.Unpriced:
                            borderColor = 0xFF00FFFF; // Yellow
                            valueTextColor = 0xFF00FFFF;
                            break;
                        default:
                            borderColor = 0xFFFFFFFF; // White
                            valueTextColor = 0xFFFFFFFF;
                            break;
                    }

                    // Draw border around the item
                    drawList.AddRect(new System.Numerics.Vector2(pos.X, pos.Y), new System.Numerics.Vector2(pos.X + size.Width, pos.Y + size.Height), borderColor, 0, ImDrawFlags.None, 2);

                    if (haggleItem is HaggleItemAbyssJewel abyssJewel && abyssJewel.IsLoadingPrice)
                    {
                        var loadingText = "LOADING...";
                        var loadingTextSize = ImGui.CalcTextSize(loadingText);
                        var loadingPos = new System.Numerics.Vector2(pos.X + (size.Width - loadingTextSize.X) / 2, pos.Y + 2);
                        drawList.AddRectFilled(new System.Numerics.Vector2(loadingPos.X - 2, loadingPos.Y - 1), new System.Numerics.Vector2(loadingPos.X + loadingTextSize.X + 2, loadingPos.Y + loadingTextSize.Y + 1), 0xB4000000); // Semi-transparent black
                        drawList.AddText(loadingPos, 0xFF00A5FF, loadingText); // Orange
                    }
                    else
                    {
                        // Get the value to display
                        float displayValue = haggleItem.ActualValue > 0 ? haggleItem.ActualValue : haggleItem.Value;
                        string valueText = Math.Round(displayValue, 1).ToString(CultureInfo.InvariantCulture) + "c";
                        var valueTextSize = ImGui.CalcTextSize(valueText);
                        var valuePos = new System.Numerics.Vector2(pos.X + 2, pos.Y + 2);
                        drawList.AddRectFilled(new System.Numerics.Vector2(valuePos.X - 2, valuePos.Y - 1), new System.Numerics.Vector2(valuePos.X + valueTextSize.X + 2, valuePos.Y + valueTextSize.Y + 1), 0xB4000000); // Semi-transparent black
                        drawList.AddText(valuePos, valueTextColor, valueText);
                    }

                    // Get the cost to display
                    float costValue = haggleItem.Price?.TotalValue() ?? 0;
                    string costText = Math.Round(costValue, 1).ToString(CultureInfo.InvariantCulture) + "c";
                    var costTextSize = ImGui.CalcTextSize(costText);
                    var costPos = new System.Numerics.Vector2(pos.X + 2, pos.Y + size.Height - costTextSize.Y - 2);
                    drawList.AddRectFilled(new System.Numerics.Vector2(costPos.X - 2, costPos.Y - 1), new System.Numerics.Vector2(costPos.X + costTextSize.X + 2, costPos.Y + costTextSize.Y + 1), 0xB4000000); // Semi-transparent black
                    drawList.AddText(costPos, 0xFF00FFFF, costText); // Yellow
                }
            }
        }

        // Render inventory snapshot visualization when inventory is open
        if (Settings.SillyOrExperimenalFeatures.ShowInventorySnapshotVisualization &&
            GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory] is { IsVisible: true })
        {
            var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var inventoryRect = inventoryPanel.InventoryUIElement.GetClientRect();

            // Get current snapshot and baseline
            var currentSnapshot = Stash.Inventory.CreateSnapshot();
            var baselineSnapshot = Stash.Inventory.GetBaselineSnapshot();

            if (baselineSnapshot != null)
            {
                // Calculate cell size based on inventory dimensions
                float cellWidth = inventoryRect.Width / 12f;
                float cellHeight = inventoryRect.Height / 5f;

                // Draw snapshot information
                for (int x = 0; x < 12; x++)
                {
                    for (int y = 0; y < 5; y++)
                    {
                        var cellRect = new RectangleF(
                            inventoryRect.X + x * cellWidth,
                            inventoryRect.Y + y * cellHeight,
                            cellWidth,
                            cellHeight
                        );

                        bool isCurrentlyOccupied = currentSnapshot.IsOccupied(x, y);
                        bool wasInBaseline = baselineSnapshot.IsOccupied(x, y);

                        if (isCurrentlyOccupied)
                        {
                            Color cellColor;
                            string label = "";

                            if (wasInBaseline)
                            {
                                // Item was in baseline (existing item)
                                cellColor = Color.Blue;
                                label = "BASE";
                            }
                            else
                            {
                                // Item is new since baseline
                                cellColor = Color.Green;
                                label = "NEW";
                            }

                            // Draw colored background
                            var bgColorObj = new Color(cellColor.R, cellColor.G, cellColor.B, (byte)100);
                            uint bgColor = ImGui.GetColorU32(new System.Numerics.Vector4(bgColorObj.R / 255f, bgColorObj.G / 255f, bgColorObj.B / 255f, bgColorObj.A / 255f));
                            uint frameColor = ImGui.GetColorU32(new System.Numerics.Vector4(cellColor.R / 255f, cellColor.G / 255f, cellColor.B / 255f, cellColor.A / 255f));
                            drawList.AddRectFilled(new System.Numerics.Vector2(cellRect.Left, cellRect.Top), new System.Numerics.Vector2(cellRect.Right, cellRect.Bottom), bgColor);
                            drawList.AddRect(new System.Numerics.Vector2(cellRect.Left, cellRect.Top), new System.Numerics.Vector2(cellRect.Right, cellRect.Bottom), frameColor, 0, ImDrawFlags.None, 2);


                            // Draw label
                            var labelSize = ImGui.CalcTextSize(label);
                            var labelX = cellRect.X + (cellRect.Width - labelSize.X) / 2;
                            var labelY = cellRect.Y + (cellRect.Height - labelSize.Y) / 2;
                            drawList.AddText(new System.Numerics.Vector2(labelX, labelY), 0xFFFFFFFF, label);
                        }
                        else if (wasInBaseline)
                        {
                            // Slot was occupied in baseline but is now empty (item was moved)
                            Color cellColor = Color.Red;
                            string label = "MOVED";

                            // Draw colored background
                            var bgColorObj = new Color(cellColor.R, cellColor.G, cellColor.B, (byte)100);
                            uint bgColor = ImGui.GetColorU32(new System.Numerics.Vector4(bgColorObj.R / 255f, bgColorObj.G / 255f, bgColorObj.B / 255f, bgColorObj.A / 255f));
                            uint frameColor = ImGui.GetColorU32(new System.Numerics.Vector4(cellColor.R / 255f, cellColor.G / 255f, cellColor.B / 255f, cellColor.A / 255f));
                            drawList.AddRectFilled(new System.Numerics.Vector2(cellRect.Left, cellRect.Top), new System.Numerics.Vector2(cellRect.Right, cellRect.Bottom), bgColor);
                            drawList.AddRect(new System.Numerics.Vector2(cellRect.Left, cellRect.Top), new System.Numerics.Vector2(cellRect.Right, cellRect.Bottom), frameColor, 0, ImDrawFlags.None, 2);

                            // Draw label
                            var labelSize = ImGui.CalcTextSize(label);
                            var labelX = cellRect.X + (cellRect.Width - labelSize.X) / 2;
                            var labelY = cellRect.Y + (cellRect.Height - labelSize.Y) / 2;
                            drawList.AddText(new System.Numerics.Vector2(labelX, labelY), 0xFFFFFFFF, label);
                        }
                    }
                }

                // Draw legend
                var legendX = inventoryRect.X;
                var legendY = inventoryRect.Bottom + 10;
                var legendSpacing = 150;

                drawList.AddText(new System.Numerics.Vector2(legendX, legendY), 0xFFFFFFFF, "Legend:");
                drawList.AddText(new System.Numerics.Vector2(legendX, legendY + 15), 0xFFFF0000, "BLUE = Baseline items");
                drawList.AddText(new System.Numerics.Vector2(legendX, legendY + 30), 0xFF00FF00, "GREEN = New items");
                drawList.AddText(new System.Numerics.Vector2(legendX, legendY + 45), 0xFF0000FF, "RED = Moved items");

                // Draw snapshot info
                var baselineCount = baselineSnapshot.GetAllItems().Count;
                var currentCount = currentSnapshot.GetAllItems().Count;
                var newItemsCount = Stash.Inventory.CalculateDiffFromBaseline().Count;
                var infoText = $"Baseline: {baselineCount} | Current: {currentCount} | New: {newItemsCount}";
                drawList.AddText(new System.Numerics.Vector2(legendX, legendY + 60), 0xFF00FFFF, infoText);
            }
            else
            {
                // No baseline snapshot available
                var infoText = "No baseline snapshot available. Start haggling to create one.";
                var textSize = ImGui.CalcTextSize(infoText);
                var textX = inventoryRect.X + (inventoryRect.Width - textSize.X) / 2;
                var textY = inventoryRect.Bottom + 10;
                drawList.AddText(new System.Numerics.Vector2(textX, textY), 0xFF00FFFF, infoText);
            }
        }

        // Render logbook overlay (moved to PrepareLogbook.Inventory)
        // PrepareLogbook.Inventory.RenderOverlay();

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
                drawList.AddRect(new System.Numerics.Vector2(chosenNode.Position.TopLeft.X, chosenNode.Position.TopLeft.Y), new System.Numerics.Vector2(chosenNode.Position.BottomRight.X, chosenNode.Position.BottomRight.Y), 0xFF00A5FF, 0, ImDrawFlags.None, 3);
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
                drawList.AddText(new System.Numerics.Vector2(textPos.X, textPos.Y), 0xFF0000FF, "WARNING: Some artifacts are disabled");
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
            var txt = "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana Vorana !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";
            txt += txt;
            txt += txt;
            var textSize = ImGui.CalcTextSize(txt);

            if (_areaHasVoranaJumps % 10 == 0)
            {
                _areaHasChangedVoranaColor = _areaHasChangedVoranaColor == Color.Red ? Color.Yellow : Color.Red;
            }


            var drawNum = (screenHeight - (int)(screenHeight * 0.2)) / textSize.Y;
            for (int i = 0; i < drawNum; i++)
            {
                var y = screenHeight / drawNum * i;
                var voranaColor = ImGui.GetColorU32(new System.Numerics.Vector4(_areaHasChangedVoranaColor.R / 255f, _areaHasChangedVoranaColor.G / 255f, _areaHasChangedVoranaColor.B / 255f, _areaHasChangedVoranaColor.A / 255f));
                drawList.AddText(new System.Numerics.Vector2(screenWidth / 2 - textSize.X / 2, y), voranaColor, txt);
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

    public bool IsLogbookReady(PrepareLogbook.Logbook logbook, PrepareLogbookSettings settings)
    {
        // Check if logbook is corrupted
        if (logbook.IsCorrupted)
            return false;

        // Check if logbook is identified
        if (!logbook.IsIdentified)
            return false;

        // Check quantity threshold
        var quantity = logbook.Quantity ?? 0;
        if (quantity < settings.MinQuantity)
            return false;

        // Check for bad mods
        var badMods = settings.ModsBlackList.Value.Split(',').Select(x => x.ToLower()).ToList();
        if (logbook.Mods.Any(entry => badMods.Any(term => entry.Contains(term))))
            return false;

        // For T17+ maps, check additional criteria
        var mapInfo = logbook.Data?.MapInfo;
        if (mapInfo != null && mapInfo.Tier >= 17)
        {
            bool shouldCheckScarabs = settings.MinScarabsT17 >= 1;
            bool shouldCheckMaps = settings.MinMapsT17 >= 1;
            bool shouldCheckCurrency = settings.MinCurrencyT17 >= 1;
            bool shouldCheckPackSize = settings.MinPackSizeT17 >= 1;

            bool hasEnoughScarabs = mapInfo.MoreScarabs >= settings.MinScarabsT17;
            bool hasEnoughMaps = mapInfo.MoreMaps >= settings.MinMapsT17;
            bool hasEnoughCurrency = mapInfo.MoreCurrency >= settings.MinCurrencyT17;
            bool hasEnoughPackSize = mapInfo.PackSize >= settings.MinPackSizeT17;

            if (settings.MinT17OrMode)
            {
                // In OR mode, any one criterion being met is enough
                if (shouldCheckScarabs && hasEnoughScarabs) return true;
                if (shouldCheckMaps && hasEnoughMaps) return true;
                if (shouldCheckCurrency && hasEnoughCurrency) return true;
                if (shouldCheckPackSize && hasEnoughPackSize) return true;

                // If none of the enabled criteria are met, it's not ready
                return false;
            }
            else
            {
                // In AND mode, all enabled criteria must be met
                if (shouldCheckScarabs && !hasEnoughScarabs) return false;
                if (shouldCheckMaps && !hasEnoughMaps) return false;
                if (shouldCheckCurrency && !hasEnoughCurrency) return false;
                if (shouldCheckPackSize && !hasEnoughPackSize) return false;
            }
        }

        // For logbooks, check if they need blessing
        if (logbook.Rarity == ItemRarity.Rare && !logbook.IsBlessed)
            return false;

        return true;
    }
}