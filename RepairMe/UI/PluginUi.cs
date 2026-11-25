using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;
using UiConstants = RepairMe.RepairMeConstants.Ui;

namespace RepairMe
{
    internal partial class PluginUi : IDisposable
    {
        internal readonly UiConstants.PluginUiDefaults _const = UiConstants.CreatePluginUiDefaults();

        // reference fields
        /// <summary>Cached configuration reference — loaded once in constructor to avoid repeated GetOrLoad() lock contention.</summary>
        private readonly Configuration _conf;
        /// <summary>Access to cached configuration.</summary>
        private Configuration Conf => _conf;
        private readonly EquipmentTracker tracker;

        private static IPlayerState PlayerState => RepairMe.PlayerState;

        private readonly IDalamudPluginInterface _pluginInterface;

        internal PluginUi(EquipmentTracker tracker, IDalamudPluginInterface pluginInterface)
        {
            this.tracker = tracker;
            _pluginInterface = pluginInterface;
            _conf = Configuration.GetOrLoad();
        }

        // Derived equipment view; recomputed only when the equipment or the item-level filter changes.
        private int _viewVersion = -1;
        private int _viewMinItemLevel = -1;
        private float _viewCondition = UiConstants.DefaultConditionPercent;
        private int _viewConditionSlot;
        private SpiritbondFilterResult _spiritbondView = SpiritbondFilterResult.Empty;
        private (int Version, ushort Job, byte Level, bool HasItems) _missingGearKey = (-1, 0, 0, false);
        private MissingGearResult _missingGearResult = new(false, string.Empty);

        private readonly ResolutionOptionsCache _resolutionOptions = new();

        private bool TestingActive => _const.settingsVisible && _const._testingMode;

        internal void OnEquipmentChanged()
        {
            RefreshEquipmentView();
            if (!TestingActive)
                UpdateLiveDtr();
        }

        private void RefreshEquipmentView()
        {
            var version = tracker.EquipmentVersion;
            var minLevel = Conf.MinSpiritbondItemLevel;
            if (version == _viewVersion && minLevel == _viewMinItemLevel)
                return;

            // Don't cache a result computed without the item sheet; retry next call.
            if (_const.Items != null)
            {
                _viewVersion = version;
                _viewMinItemLevel = minLevel;
            }

            var e = tracker.GetLastEquipmentData();
            _viewCondition = e?.LowestConditionPercent ?? UiConstants.DefaultConditionPercent;
            _viewConditionSlot = e?.LowestConditionSlot ?? 0;
            _spiritbondView = EquipmentAnalysis.FilterSpiritbondByItemLevel(e, minLevel, _const.Items);
        }

        private void UpdateLiveDtr()
            => UpdateDtrBar(_viewCondition, _viewConditionSlot, _spiritbondView.Highest ?? 0f, _spiritbondView.HighestSlot,
                Conf.ShowDtrConditionLabel, Conf.ShowDtrSpiritbondLabel);

        public void Draw()
        {
            try
            {
                if (!tracker.IsActive) return;

                // Casting ImGuiVector directly to int truncates toward zero, same as floor+cast for positive values.
                int resolutionWidth = (int)ImGui.GetMainViewport().Size.X;
                int resolutionHeight = (int)ImGui.GetMainViewport().Size.Y;
                string resolutionId = $"{resolutionWidth}x{resolutionHeight}";

                if (Conf.PositionProfiles.GetValueOrDefault(resolutionId) is not PositionProfile positionProfile)
                {
                    positionProfile = new PositionProfile
                    {
                        ResolutionWidth = resolutionWidth,
                        ResolutionHeight = resolutionHeight
                    };
                    if (Conf.PositionProfiles.TryAdd(resolutionId, positionProfile))
                    {
                        _resolutionOptions.Invalidate();
                        Conf.Save();
                    }
                }

                _const.position = positionProfile;

                if (_const._testingMode && _const.isUiUnlocked)
                {
                    // Unlocked UI mode: skip all equipment updates
                }
                else if (_const.settingsVisible && _const._testingMode && !_const.isUiUnlocked)
                {
                    var cycleDuration = UiConstants.TestingModeCycleDuration + 2f;
                    var elapsed = (float)_const._testingTimer.Elapsed.TotalSeconds;
                    var cyclePos = elapsed % cycleDuration;
                    float adjustedProgress;
                    if (cyclePos >= UiConstants.TestingModeCycleDuration)
                    {
                        adjustedProgress = 1f;
                    }
                    else
                    {
                        adjustedProgress = cyclePos / UiConstants.TestingModeCycleDuration;
                    }
                    _const.condition = (1f - adjustedProgress) * 100f;
                    _const.spiritbond = Math.Min((adjustedProgress + 0.01f) * 100f, 100f);
                    _const.leastSpiritbond = _const.spiritbond * 0.3f;
                    _const.spiritbondPoints = new[] { 0f, 0.2f, 0.25f, 0.4f, 0.5f, 0f, 0.7f, 0.8f, 0.81f, 0.82f, 0.83f, 0.9f };
                    UpdateDtrBar(_const.condition, 0, _const.spiritbond, 0, Conf.ShowDtrConditionLabel, Conf.ShowDtrSpiritbondLabel);
                }
                else
                {
                    // Detect transition from testing mode to live mode
                    if (_const._prevTestingMode && !_const._testingMode)
                    {
                        // Force DTR cache reset by passing negative slot
                        UpdateDtrBar(-1f, -1, -1f, -1, false, false);
                    }

                    RefreshEquipmentView();
                    _const.condition = _viewCondition;
                    _const.spiritbond = _spiritbondView.Highest ?? 0f;
                    _const.leastSpiritbond = _spiritbondView.Lowest ?? 0f;
                    _const.spiritbondPoints = _spiritbondView.Points.Length > 0 ? _spiritbondView.Points : UiConstants.EmptyPoints;
                    UpdateLiveDtr();
                }

                // Track previous frame state for transition detection
                _const._prevTestingMode = _const._testingMode;

                // Detect resolution change and reset first-frame flag
                if (_const._currentResolutionId != resolutionId)
                {
                    _const._currentResolutionId = resolutionId;
                    _const.isDrawingFirstFrame = true;
                }

                _const.overlayBuilder ??= CreateOverlays();

                ComputeConditionAlertState();
                ComputeMissingGearAlertState();
                ComputeSpiritbondAlertState();

                _const.overlayBuilder.Draw();

                DrawSettingsWindow();
                DrawDebugWindow();
                _const.windowSystem.Draw();
                SyncWindowVisibility();

                if (_const.isDrawingFirstFrame && !Conf.PositionsMigrated)
                {
                    Conf.PositionsMigrated = true;
                    Conf.Save();
                }

                _const.isDrawingFirstFrame = false;
            }
            catch (Exception ex)
            {
                RepairMe.Log.Error(ex, "prevented GUI crash");
            }
        }

        private OverlayBuilder CreateOverlays()
        {
            return new OverlayBuilder
            {
                Items = new OverlayItem[]
                {
                    new BarOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.BarConditionEnabled,
                        positionProvider: () => _const.position.BarCondition,
                        positionWriter: v => _const.position.BarCondition = v,
                        windowName: "repairme-bar-condition",
                        sizeProvider: () => Conf.BarConditionSize,
                        orientationProvider: () => Conf.BarConditionOrientation,
                        roundingProvider: () => Conf.BarConditionRounding,
                        borderSizeProvider: () => Conf.BarConditionBorderSize,
                        borderColorProvider: () => Conf.BarConditionBorderColor,
                        stateProvider: () => SelectConditionBarState(_const.condition)),

                    new BarOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.BarSpiritbondEnabled,
                        positionProvider: () => _const.position.BarSpiritbond,
                        positionWriter: v => _const.position.BarSpiritbond = v,
                        windowName: "repairme-bar-spiritbond",
                        sizeProvider: () => Conf.BarSpiritbondSize,
                        orientationProvider: () => Conf.BarSpiritbondOrientation,
                        roundingProvider: () => Conf.BarSpiritbondRounding,
                        borderSizeProvider: () => Conf.BarSpiritbondBorderSize,
                        borderColorProvider: () => Conf.BarSpiritbondBorderColor,
                        stateProvider: () => SelectSpiritbondBarState(_const.spiritbond)),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.PercentConditionEnabled,
                        textProvider: () => FormatPercentText(
                            _const.condition,
                            null,
                            Conf.PercentConditionShowPercent,
                            Conf.PercentConditionShowDecimals),
                        positionProvider: () => _const.position.PercentCondition,
                        positionWriter: v => _const.position.PercentCondition = v,
                        windowName: "repairme-percent-condition",
                        colorProvider: () => Conf.PercentConditionColor,
                        backgroundProvider: () => Conf.PercentConditionBg,
                        useMinimumWindowSize: false,
                        scaleProvider: () => null,
                        clickActionProvider: () => null),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.PercentSpiritbondEnabled,
                        textProvider: () => FormatPercentText(
                            _const.spiritbond,
                            Conf.PercentSpiritbondShowMinMax ? _const.leastSpiritbond : null,
                            Conf.PercentSpiritbondShowPercent,
                            Conf.PercentSpiritbondShowDecimals),
                        positionProvider: () => _const.position.PercentSpiritbond,
                        positionWriter: v => _const.position.PercentSpiritbond = v,
                        windowName: "repairme-percent-spiritbond",
                        colorProvider: () => Conf.PercentSpiritbondColor,
                        backgroundProvider: () => Conf.PercentSpiritbondBg,
                        useMinimumWindowSize: false,
                        scaleProvider: () => null,
                        clickActionProvider: () => null),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated
                                            && OverlayVisibility.ShouldShowAlert(AnyConditionAlertEnabled(), HasActiveConditionAlert(), _const.isUiUnlocked),
                        textProvider: () => _conditionAlertCache.IsActive ? _conditionAlertCache.Text : Conf.AlertConditionLowText,
                        positionProvider: () => _const.position.AlertCondition,
                        positionWriter: v => _const.position.AlertCondition = v,
                        windowName: "repairme-alert-condition",
                        colorProvider: () => _conditionAlertCache.IsActive ? _conditionAlertCache.Color : Conf.AlertConditionLowColor,
                        backgroundProvider: () => _conditionAlertCache.IsActive ? _conditionAlertCache.Background : Conf.AlertConditionLowBg,
                        useMinimumWindowSize: true,
                        scaleProvider: () => Conf.AlertConditionScale,
                        clickActionProvider: () => _conditionAlertCache.ClickAction),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated
                                            && OverlayVisibility.ShouldShowAlert(Conf.AlertSpiritbondFullEnabled,
                                                _const.spiritbond >= UiConstants.SpiritbondFullThreshold, _const.isUiUnlocked),
                        textProvider: () => Conf.AlertSpiritbondFullText,
                        positionProvider: () => _const.position.AlertSpiritbond,
                        positionWriter: v => _const.position.AlertSpiritbond = v,
                        windowName: "repairme-alert-spiritbond",
                        colorProvider: () => Conf.AlertSpiritbondFullColor,
                        backgroundProvider: () => Conf.AlertSpiritbondFullBg,
                        useMinimumWindowSize: true,
                        scaleProvider: () => Conf.AlertSpiritbondFullScale,
                        clickActionProvider: () => Conf.AlertSpiritbondShortcut ? (Action?)OpenMateriaExtraction : null),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.ConditionLabelEnabled,
                        textProvider: () => Conf.ConditionLabelText,
                        positionProvider: () => _const.position.ConditionLabel,
                        positionWriter: v => _const.position.ConditionLabel = v,
                        windowName: "repairme-condition-label",
                        colorProvider: () => Conf.ConditionLabelColor,
                        backgroundProvider: () => Conf.ConditionLabelBg,
                        useMinimumWindowSize: false,
                        scaleProvider: () => Conf.ConditionLabelScale,
                        clickActionProvider: () => null),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated && Conf.SpiritbondLabelEnabled,
                        textProvider: () => Conf.SpiritbondLabelText,
                        positionProvider: () => _const.position.SpiritbondLabel,
                        positionWriter: v => _const.position.SpiritbondLabel = v,
                        windowName: "repairme-spiritbond-label",
                        colorProvider: () => Conf.SpiritbondLabelColor,
                        backgroundProvider: () => Conf.SpiritbondLabelBg,
                        useMinimumWindowSize: false,
                        scaleProvider: () => Conf.SpiritbondLabelScale,
                        clickActionProvider: () => null),

                    new TextOverlayItem(this,
                        shouldRender: () => Conf.PositionsMigrated
                                            && OverlayVisibility.ShouldShowAlert(Conf.AlertMissingGearEnabled, HasMissingGearAlert(), _const.isUiUnlocked),
                        textProvider: () => _missingGearAlertCache.IsActive
                            ? _missingGearAlertCache.Text
                            : Conf.AlertMissingGearText.Replace("{slot}", EquipmentConstants.SlotNames[EquipmentConstants.SlotHead]),
                        positionProvider: () => _const.position.AlertMissingGear,
                        positionWriter: v => _const.position.AlertMissingGear = v,
                        windowName: "repairme-alert-missing-gear",
                        colorProvider: () => _missingGearAlertCache.IsActive ? _missingGearAlertCache.Color : Conf.AlertMissingGearColor,
                        backgroundProvider: () => _missingGearAlertCache.IsActive ? _missingGearAlertCache.Background : Conf.AlertMissingGearBg,
                        useMinimumWindowSize: true,
                        scaleProvider: () => Conf.AlertMissingGearScale,
                        clickActionProvider: () => Conf.AlertMissingGearShortcut ? (Action?)OpenRepairs : null),
                }
            };
        }


        // =====================================================================
        // Declarative Settings Pipeline (new)
        // =====================================================================

        private WindowBuilder CreateSettingsWindow()
        {
            var c = Conf;

            return new WindowBuilder
            {
                Title = "RepairMe Settings",
                InitialSize = new Vector2(510, 525),
                InitialSizeCondition = ImGuiCond.FirstUseEver,
                DisableWindowSounds = false,
                OnOpenSfxId = 23,
                OnCloseSfxId = 24,
                UseTabBar = true,
                HeaderItems =
                [
                    UiItem.SameLine(
                        UiItem.Checkbox("Move UI (pauses testing mode)##repairMe001", () => _const.UnlockedUiModeCheckbox,
                            v => _const.UnlockedUiModeCheckbox = v, c.Save),
                        UiItem.Checkbox("Testing Mode##repairMe002", () => _const._testingMode,
                            v => _const._testingMode = v, c.Save),
                        UiItem.Checkbox("Hide when player is occupied##repairMe003", () => c.HideUiWhenOccupied,
                            v => c.HideUiWhenOccupied = v, c.Save)),
                    UiItem.Spacing(),
                ],
                Tabs =
                [
                    UiItem.Tab("Condition##repairMe081",
                        UiItem.Section("Toggles", ImGuiTreeNodeFlags.DefaultOpen,
                                    UiItem.Checkbox("Show Bar##cond-bar", () => c.BarConditionEnabled, v => c.BarConditionEnabled = v, c.Save),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show Percentage##cond-pct", () => c.PercentConditionEnabled, v => c.PercentConditionEnabled = v, c.Save),
                                        UiItem.Checkbox("Show Decimals##cond-dec", () => c.PercentConditionShowDecimals, v => c.PercentConditionShowDecimals = v, c.Save),
                                        UiItem.Checkbox("Show % Sign##cond-sign", () => c.PercentConditionShowPercent, v => c.PercentConditionShowPercent = v, c.Save)),
                                    UiItem.Checkbox("Show DTR Condition Label##cond-dtr", () => c.ShowDtrConditionLabel, v => c.ShowDtrConditionLabel = v, c.Save),
                                    UiItem.Checkbox("Show Condition Overlay Label##cond-label", () => c.ConditionLabelEnabled, v => c.ConditionLabelEnabled = v, c.Save),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show alert when low##cond-low", () => c.AlertConditionLowEnabled, v => c.AlertConditionLowEnabled = v, c.Save),
                                        UiItem.Checkbox("Clicking alert opens repairs##cond-low-sc", () => c.AlertConditionLowShortcut, v => c.AlertConditionLowShortcut = v, c.Save)),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show alert when critical##cond-crit", () => c.AlertConditionCriticalEnabled, v => c.AlertConditionCriticalEnabled = v, c.Save),
                                        UiItem.Checkbox("Clicking alert opens repairs##cond-crit-sc", () => c.AlertConditionCriticalShortcut, v => c.AlertConditionCriticalShortcut = v, c.Save)),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show alert when repairable##cond-repair", () => c.AlertConditionRepairableEnabled, v => c.AlertConditionRepairableEnabled = v, c.Save),
                                        UiItem.Checkbox("Clicking alert opens repairs##cond-repair-sc", () => c.AlertConditionRepairableShortcut, v => c.AlertConditionRepairableShortcut = v, c.Save))),
                        UiItem.Section("Text", ImGuiTreeNodeFlags.None,
                                    UiItem.Width(180, UiItem.TextInput("Equipment Repairable Text##cond-txt1", "", () => c.AlertConditionRepairableText, v => c.AlertConditionRepairableText = v, c.Save, UiConstants.AlertMessageMaximumLength)),
                                    UiItem.Width(180, UiItem.TextInput("Equipment Low Text##cond-txt2", "", () => c.AlertConditionLowText, v => c.AlertConditionLowText = v, c.Save, UiConstants.AlertMessageMaximumLength)),
                                    UiItem.Width(180, UiItem.TextInput("Equipment Critical Text##cond-txt3", "", () => c.AlertConditionCriticalText, v => c.AlertConditionCriticalText = v, c.Save, UiConstants.AlertMessageMaximumLength)),
                                    UiItem.Width(180, UiItem.TextInput("Condition Overlay Label Text##cond-txt4", "", () => c.ConditionLabelText, v => c.ConditionLabelText = v, c.Save, 100, ImGuiInputTextFlags.CharsNoBlank))),
                        UiItem.Section("Thresholds", ImGuiTreeNodeFlags.None,
                                    UiItem.Text("Threshold - Order is OK > Low > Critical. This affects the condition bar and alerts"),
                                    UiItem.SliderInt("Low Threshold##cond-low", () => c.ThresholdConditionLow, v => {
                                        if (v <= c.ThresholdConditionCritical) c.ThresholdConditionLow = c.ThresholdConditionCritical + 1;
                                        else c.ThresholdConditionLow = v;
                                    }, 0, 100, "%d", c.Save),
                                    UiItem.SliderInt("Critical Threshold##cond-critical", () => c.ThresholdConditionCritical, v => {
                                        if (v >= c.ThresholdConditionLow) c.ThresholdConditionCritical = c.ThresholdConditionLow - 1;
                                        else c.ThresholdConditionCritical = v;
                                    }, 0, 100, "%d", c.Save),
                                    UiItem.Spacing(),
                                    UiItem.Text("Configure alternative alert thresholds for specific characters"),
                                    UiItem.SliderInt("Low Threshold Alt##cond-low-alt", () => c.ThresholdConditionLowAlt, v => {
                                        if (v <= c.ThresholdConditionCriticalAlt) c.ThresholdConditionLowAlt = c.ThresholdConditionCriticalAlt + 1;
                                        else c.ThresholdConditionLowAlt = v;
                                    }, 0, 100, "%d", c.Save),
                                    UiItem.SliderInt("Critical Threshold Alt##cond-critical-alt", () => c.ThresholdConditionCriticalAlt, v => {
                                        if (v >= c.ThresholdConditionLowAlt) c.ThresholdConditionCriticalAlt = c.ThresholdConditionLowAlt - 1;
                                        else c.ThresholdConditionCriticalAlt = v;
                                    }, 0, 100, "%d", c.Save),
                                    UiItem.Spacing(),
                                    UiItem.Button("Apply to current character##cond-apply-alt", () => {
                                        if (PlayerState.IsLoaded)
                                        {
                                            c.AltCharacters[PlayerState.ContentId] = PlayerState.CharacterName + " @ " + PlayerState.HomeWorld.Value;
                                            c.Save();
                                        }
                                    }),
                                    UiItem.Custom(DrawAltCharacterList)),
                        UiItem.Section("Appearance Settings", ImGuiTreeNodeFlags.None,
                                    UiItem.Text("Positions"),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##cond-alert-pos", () => _const.position.AlertCondition, v => _const.position.AlertCondition = v, c.Save),
                                        UiItem.Button("reset##cond-alertcondition-reset", () => { _const.position.AlertCondition = new PositionProfile().AlertCondition; c.Save(); }),
                                        UiItem.Text("Alert Condition Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##cond-pct-pos", () => _const.position.PercentCondition, v => _const.position.PercentCondition = v, c.Save),
                                        UiItem.Button("reset##cond-percentcondition-reset", () => { _const.position.PercentCondition = new PositionProfile().PercentCondition; c.Save(); }),
                                        UiItem.Text("Percentage Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##cond-bar-pos", () => _const.position.BarCondition, v => _const.position.BarCondition = v, c.Save),
                                        UiItem.Button("reset##cond-barcondition-reset", () => { _const.position.BarCondition = new PositionProfile().BarCondition; c.Save(); }),
                                        UiItem.Text("Bar Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##cond-label-pos", () => _const.position.ConditionLabel, v => _const.position.ConditionLabel = v, c.Save),
                                        UiItem.Button("reset##cond-conditionlabel-reset", () => { _const.position.ConditionLabel = new PositionProfile().ConditionLabel; c.Save(); }),
                                        UiItem.Text("Overlay Label Position")),
                                    UiItem.DragFloat2("Bar Size##cond-size", () => c.BarConditionSize, v => c.BarConditionSize = v, c.Save, 1f, 1f),
                                    UiItem.DragFloat("Bar Rounding##cond-round", () => c.BarConditionRounding, v => c.BarConditionRounding = v, c.Save, 1f, 0f),
                                    UiItem.DragFloat("Border Size##cond-border", () => c.BarConditionBorderSize, v => c.BarConditionBorderSize = v, c.Save, 1f, 0f),
                                    UiItem.Combo("Bar Orientation##cond-orient", () => c.BarConditionOrientation,
                                        UiConstants.OrientationLabels,
                                        v => { c.BarConditionOrientation = v; c.Save(); }),
                                    UiItem.Spacing(),
                                    UiItem.Text("Alert Scale (1.0 = default, adjust to resize)"),
                                    UiItem.DragFloat("Alert Scale##cond-scale", () => c.AlertConditionScale, v => c.AlertConditionScale = Math.Clamp(v, 0.1f, 3.0f), c.Save, 0.05f, 0.1f, 3.0f, "%.2f"),
                                    UiItem.Spacing(),
                                    UiItem.Text("Overlay Label Scale (1.0 = default, adjust to resize)"),
                                    UiItem.DragFloat("Label Scale##cond-labelscale", () => c.ConditionLabelScale, v => c.ConditionLabelScale = Math.Clamp(v, 0.1f, 3.0f), c.Save, 0.05f, 0.1f, 3.0f, "%.2f"),
                                    UiItem.Spacing(),
                                    UiItem.Text("Colors - They support transparency, including becoming fully transparent"),
                                    UiItem.ColorTable("cond-colors", 2,
                                        UiItem.Color("Percentage Color", () => c.PercentConditionColor, c2 => c.PercentConditionColor = c2, c.Save),
                                        UiItem.Color("Percentage Background", () => c.PercentConditionBg, c2 => c.PercentConditionBg = c2, c.Save),
                                        UiItem.Color("Bar Color", () => c.BarConditionOkColor, c2 => c.BarConditionOkColor = c2, c.Save),
                                        UiItem.Color("Bar Background", () => c.BarConditionOkBackground, c2 => c.BarConditionOkBackground = c2, c.Save),
                                        UiItem.Color("Bar Low Color", () => c.BarConditionLowColor, c2 => c.BarConditionLowColor = c2, c.Save),
                                        UiItem.Color("Bar Low Background", () => c.BarConditionLowBackground, c2 => c.BarConditionLowBackground = c2, c.Save),
                                        UiItem.Color("Bar Critical Color", () => c.BarConditionCriticalColor, c2 => c.BarConditionCriticalColor = c2, c.Save),
                                        UiItem.Color("Bar Critical Background", () => c.BarConditionCriticalBackground, c2 => c.BarConditionCriticalBackground = c2, c.Save),
                                        UiItem.Color("Bar Border Color", () => c.BarConditionBorderColor, c2 => c.BarConditionBorderColor = c2, c.Save),
                                        UiItem.Blank(),
                                        UiItem.Color("Alert Repairable Text Color", () => c.AlertConditionRepairableColor, c2 => c.AlertConditionRepairableColor = c2, c.Save),
                                        UiItem.Color("Alert Repairable Background", () => c.AlertConditionRepairableBg, c2 => c.AlertConditionRepairableBg = c2, c.Save),
                                        UiItem.Color("Alert Low Text Color", () => c.AlertConditionLowColor, c2 => c.AlertConditionLowColor = c2, c.Save),
                                        UiItem.Color("Alert Low Background", () => c.AlertConditionLowBg, c2 => c.AlertConditionLowBg = c2, c.Save),
                                        UiItem.Color("Alert Critical Text Color", () => c.AlertConditionCriticalColor, c2 => c.AlertConditionCriticalColor = c2, c.Save),
                                        UiItem.Color("Alert Critical Background", () => c.AlertConditionCriticalBg, c2 => c.AlertConditionCriticalBg = c2, c.Save),
                                        UiItem.Color("Condition Overlay Label Color", () => c.ConditionLabelColor, c2 => c.ConditionLabelColor = c2, c.Save),
                                        UiItem.Color("Condition Overlay Label Background", () => c.ConditionLabelBg, c2 => c.ConditionLabelBg = c2, c.Save)
                                    ))),
                    UiItem.Tab("Spiritbond##repairMe083",
                        UiItem.Section("Toggles", ImGuiTreeNodeFlags.DefaultOpen,
                                    UiItem.Checkbox("Show Bar##sb-bar", () => c.BarSpiritbondEnabled, v => c.BarSpiritbondEnabled = v, c.Save),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show Percentage##sb-pct", () => c.PercentSpiritbondEnabled, v => c.PercentSpiritbondEnabled = v, c.Save),
                                        UiItem.Checkbox("Show Decimals##sb-dec", () => c.PercentSpiritbondShowDecimals, v => c.PercentSpiritbondShowDecimals = v, c.Save),
                                        UiItem.Checkbox("Show % Sign##sb-sign", () => c.PercentSpiritbondShowPercent, v => c.PercentSpiritbondShowPercent = v, c.Save),
                                        UiItem.Checkbox("Also show least spiritbonded item##sb-minmax", () => c.PercentSpiritbondShowMinMax, v => c.PercentSpiritbondShowMinMax = v, c.Save)),
                                    UiItem.Checkbox("Show DTR Spiritbond Label##sb-dtr", () => c.ShowDtrSpiritbondLabel, v => c.ShowDtrSpiritbondLabel = v, c.Save),
                                    UiItem.Checkbox("Show Spiritbond Overlay Label##sb-label", () => c.SpiritbondLabelEnabled, v => c.SpiritbondLabelEnabled = v, c.Save),
                                    UiItem.SameLine(
                                        UiItem.Checkbox("Show alert when full##sb-full", () => c.AlertSpiritbondFullEnabled, v => c.AlertSpiritbondFullEnabled = v, c.Save),
                                        UiItem.Checkbox("Clicking alert opens spiritbond##sb-full-sc", () => c.AlertSpiritbondShortcut, v => c.AlertSpiritbondShortcut = v, c.Save))),
                        UiItem.Section("Text", ImGuiTreeNodeFlags.None,
                                    UiItem.Width(180, UiItem.TextInput("Spiritbond Full Text##sb-txt1", "", () => c.AlertSpiritbondFullText, v => c.AlertSpiritbondFullText = v, c.Save, UiConstants.AlertMessageMaximumLength)),
                                    UiItem.Width(180, UiItem.TextInput("Spiritbond Overlay Label Text##sb-txt2", "", () => c.SpiritbondLabelText, v => c.SpiritbondLabelText = v, c.Save, 100, ImGuiInputTextFlags.CharsNoBlank)),
                                    UiItem.Width(60, UiItem.TextInput("Min item level to track##sb-minlvl", "",
                                        () => c.MinSpiritbondItemLevel.ToString(),
                                        v => {
                                            if (int.TryParse(v, out var value) && value >= 1 && value < 1000)
                                            {
                                                c.MinSpiritbondItemLevel = value;
                                                c.Save();
                                            }
                                        }, maxLength: 4, flags: ImGuiInputTextFlags.CharsDecimal)),
                                    UiItem.Text("Enter a whole number from 1 to 999.")),
                        UiItem.Section("Appearance Settings", ImGuiTreeNodeFlags.None,
                                    UiItem.Text("Positions"),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##sb-alert-pos", () => _const.position.AlertSpiritbond, v => _const.position.AlertSpiritbond = v, c.Save),
                                        UiItem.Button("reset##sb-alert-reset", () => { _const.position.AlertSpiritbond = new PositionProfile().AlertSpiritbond; c.Save(); }),
                                        UiItem.Text("Alert Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##sb-pct-pos", () => _const.position.PercentSpiritbond, v => _const.position.PercentSpiritbond = v, c.Save),
                                        UiItem.Button("reset##sb-pct-reset", () => { _const.position.PercentSpiritbond = new PositionProfile().PercentSpiritbond; c.Save(); }),
                                        UiItem.Text("Percentage Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##sb-label-pos", () => _const.position.SpiritbondLabel, v => _const.position.SpiritbondLabel = v, c.Save),
                                        UiItem.Button("reset##sb-label-reset", () => { _const.position.SpiritbondLabel = new PositionProfile().SpiritbondLabel; c.Save(); }),
                                        UiItem.Text("Overlay Label Position")),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##sb-bar-pos", () => _const.position.BarSpiritbond, v => _const.position.BarSpiritbond = v, c.Save),
                                        UiItem.Button("reset##sb-bar-reset", () => { _const.position.BarSpiritbond = new PositionProfile().BarSpiritbond; c.Save(); }),
                                        UiItem.Text("Bar Position")),
                                    UiItem.DragFloat2("Bar Size##sb-size", () => c.BarSpiritbondSize, v => c.BarSpiritbondSize = v, c.Save, 1f, 1f),
                                    UiItem.DragFloat("Bar Rounding##sb-round", () => c.BarSpiritbondRounding, v => c.BarSpiritbondRounding = v, c.Save, 1f, 0f),
                                    UiItem.DragFloat("Border Size##sb-border", () => c.BarSpiritbondBorderSize, v => c.BarSpiritbondBorderSize = v, c.Save, 1f, 0f),
                                    UiItem.Combo("Bar Orientation##sb-orient", () => c.BarSpiritbondOrientation,
                                        UiConstants.OrientationLabels,
                                        v => { c.BarSpiritbondOrientation = v; c.Save(); }),
                                    UiItem.Spacing(),
                                    UiItem.Checkbox("Show all items in bar##sb-all", () => c.BarSpiritbondShowAllItems, v => c.BarSpiritbondShowAllItems = v, c.Save),
                                    UiItem.Spacing(),
                                    UiItem.Text("Alert Scale (1.0 = default, adjust to resize)"),
                                    UiItem.DragFloat("Alert Scale##sb-scale", () => c.AlertSpiritbondFullScale,
                                        v => c.AlertSpiritbondFullScale = Math.Clamp(v, 0.1f, 3.0f),
                                        c.Save, 0.05f, 0.1f, 3.0f, "%.2f"),
                                    UiItem.Spacing(),
                                    UiItem.Text("Overlay Label Scale (1.0 = default, adjust to resize)"),
                                    UiItem.DragFloat("Label Scale##sb-labelscale", () => c.SpiritbondLabelScale,
                                        v => c.SpiritbondLabelScale = Math.Clamp(v, 0.1f, 3.0f),
                                        c.Save, 0.05f, 0.1f, 3.0f, "%.2f"),
                                    UiItem.Spacing(),
                                    UiItem.Text("Colors - They support transparency, including becoming fully transparent"),
                                    UiItem.ColorTable("sb-colors", 2,
                                        UiItem.Color("Percentage Color", () => c.PercentSpiritbondColor, c2 => c.PercentSpiritbondColor = c2, c.Save),
                                        UiItem.Color("Percentage Background", () => c.PercentSpiritbondBg, c2 => c.PercentSpiritbondBg = c2, c.Save),
                                        UiItem.Color("Bar Color", () => c.BarSpiritbondProgressColor, c2 => c.BarSpiritbondProgressColor = c2, c.Save),
                                        UiItem.Color("Bar Background", () => c.BarSpiritbondProgressBackground, c2 => c.BarSpiritbondProgressBackground = c2, c.Save),
                                        UiItem.Color("Bar Full Color", () => c.BarSpiritbondFullColor, c2 => c.BarSpiritbondFullColor = c2, c.Save),
                                        UiItem.Color("Bar Full Background", () => c.BarSpiritbondFullBackground, c2 => c.BarSpiritbondFullBackground = c2, c.Save),
                                        UiItem.Color("Bar Border Color", () => c.BarSpiritbondBorderColor, c2 => c.BarSpiritbondBorderColor = c2, c.Save),
                                        UiItem.Color("Bar Item color", () => c.BarSpiritbondPointsColor, c2 => c.BarSpiritbondPointsColor = c2, c.Save),
                                        UiItem.Color("Alert Full Text Color", () => c.AlertSpiritbondFullColor, c2 => c.AlertSpiritbondFullColor = c2, c.Save),
                                        UiItem.Color("Alert Full Background", () => c.AlertSpiritbondFullBg, c2 => c.AlertSpiritbondFullBg = c2, c.Save),
                                        UiItem.Color("Spiritbond Overlay Label Color", () => c.SpiritbondLabelColor, c2 => c.SpiritbondLabelColor = c2, c.Save),
                                        UiItem.Color("Spiritbond Overlay Label Background", () => c.SpiritbondLabelBg, c2 => c.SpiritbondLabelBg = c2, c.Save)
                                    ))),
                    UiItem.Tab("Missing##repairMe082",
                        UiItem.Section("Toggles", ImGuiTreeNodeFlags.DefaultOpen,
                                    UiItem.Checkbox("Show alert when gear slot is empty (job-aware)##missing-tog", () => c.AlertMissingGearEnabled, v => c.AlertMissingGearEnabled = v, c.Save)),
                        UiItem.Section("Text", ImGuiTreeNodeFlags.None,
                                    UiItem.Width(180, UiItem.TextInput("Missing Gear Alert Text (use {slot} for slot name)##missing-txt", "", () => c.AlertMissingGearText, v => c.AlertMissingGearText = v, c.Save, UiConstants.AlertMessageMaximumLength))),
                        UiItem.Section("Appearance Settings", ImGuiTreeNodeFlags.None,
                                    UiItem.Text("Positions"),
                                    UiItem.SameLine(
                                        UiItem.DragFloat2("##missing-pos", () => _const.position.AlertMissingGear, v => _const.position.AlertMissingGear = v, c.Save),
                                        UiItem.Button("reset##missing-reset", () => { _const.position.AlertMissingGear = new PositionProfile().AlertMissingGear; c.Save(); }),
                                        UiItem.Text("Missing Gear Alert Position")),
                                    UiItem.Spacing(),
                                    UiItem.Text("Alert Scale (1.0 = default, adjust to resize)"),
                                    UiItem.DragFloat("Alert Scale##missing-scale", () => c.AlertMissingGearScale, v => c.AlertMissingGearScale = Math.Clamp(v, 0.1f, 3.0f), c.Save, 0.05f, 0.1f, 3.0f, "%.2f"),
                                    UiItem.Spacing(),
                                    UiItem.Text("Colors"),
                                    UiItem.ColorTable("missing-colors", 2,
                                        UiItem.Color("Missing Gear Color", () => c.AlertMissingGearColor, c2 => c.AlertMissingGearColor = c2, c.Save),
                                        UiItem.Color("Missing Gear Background", () => c.AlertMissingGearBg, c2 => c.AlertMissingGearBg = c2, c.Save)
                                    ))),
                    UiItem.Tab("Other##repairMe084",
                        UiItem.Section("Alert Sounds", ImGuiTreeNodeFlags.DefaultOpen,
                            UiItem.Text("SFX IDs are game sound effect IDs; 0 disables sound."),
                            UiItem.Spacing(),
                            CreateAlertSfxRow("Condition alert SFX##repairMe302",
                                () => c.ConditionAlertSfx,
                                v => c.ConditionAlertSfx = v,
                                c.Save,
                                "##repairMe302-play"),
                            CreateAlertSfxRow("Spiritbond alert SFX##repairMe303",
                                () => c.SpiritbondAlertSfx,
                                v => c.SpiritbondAlertSfx = v,
                                c.Save,
                                "##repairMe303-play"),
                            CreateAlertSfxRow("Missing gear alert SFX##repairMe304",
                                () => c.MissingAlertSfx,
                                v => c.MissingAlertSfx = v,
                                c.Save,
                                "##repairMe304-play"),
                            UiItem.Spacing(),
                            UiItem.Button("Reset to defaults##repairMe305", () => {
                                c.ConditionAlertSfx = 39u;
                                c.SpiritbondAlertSfx = 39u;
                                c.MissingAlertSfx = 39u;
                                c.Save();
                            })),
                        UiItem.Section("Screen Resolution/Position", ImGuiTreeNodeFlags.DefaultOpen,
                            UiItem.Text("If you changed your resolution and want to copy position settings from other resolution"),
                            UiItem.Text("Copy position settings from"),
                            UiItem.SameLine(
                                UiItem.Width(120, UiItem.Combo("##repairMe401", () => _const.selectedProfileCombo,
                                    () => _resolutionOptions.Get(Conf.PositionProfiles),
                                    v => { _const.selectedProfileCombo = v; })),
                                UiItem.Button("Copy", () => {
                                    _const.positionUndo = new PositionProfile();
                                    _const.positionUndo.CopyFrom(_const.position);
                                    var keys = _resolutionOptions.Get(Conf.PositionProfiles);
                                    if (_const.selectedProfileCombo >= 0 && _const.selectedProfileCombo < keys.Length)
                                    {
                                        var source = Conf.PositionProfiles.GetValueOrDefault(keys[_const.selectedProfileCombo]);
                                        if (source != null)
                                        {
                                            _const.position.CopyFrom(source);
                                            c.Save();
                                        }
                                    }
                                }),
                                UiItem.Button("Undo last copy", () => {
                                    if (_const.positionUndo != null)
                                    {
                                        _const.position.CopyFrom(_const.positionUndo);
                                        c.Save();
                                        _const.positionUndo = null;
                                    }
                                }))),
                        UiItem.Section("Debug", ImGuiTreeNodeFlags.None,
                            UiItem.Spacing(),
                            UiItem.IconButton(FontAwesomeIcon.Bug, "Open Debug Window", ToggleDebugVisibility)))
                ]
            };
        }

        private WindowBuilder CreateDebugWindow()
        {
            return new WindowBuilder
            {
                Title = "RepairMe Debug",
                DisableWindowSounds = false,
                OnOpenSfxId = 23,
                OnCloseSfxId = 24,
                Tabs =
                [
                    UiItem.Tab("Debug",
                        UiItem.Section("Equipment", ImGuiTreeNodeFlags.DefaultOpen,
                            UiItem.Table("equipmentTable", ImGuiTableFlags.BordersInnerH,
                                [
                                    UiItem.TableColumn("i", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("slot", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("id", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("ILvl", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("Cond", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("Spiritb", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("Name", ImGuiTableColumnFlags.WidthStretch),
                                ],
                                () =>
                                {
                                    var e = tracker.GetLastEquipmentData();
                                    return e.HasValue ? BuildEquipmentDebugRows(e.Value, _const.Items) : Array.Empty<TableRootItem.TableRow>();
                                })),
                        UiItem.Section("Resolutions & Positions", ImGuiTreeNodeFlags.None,
                            UiItem.Spacing(),
                            UiItem.Text("Resolutions & Positions"),
                            UiItem.Spacing(),
                            UiItem.Table("positionsTable", ImGuiTableFlags.BordersInnerH,
                                [
                                    UiItem.TableColumn("resolution", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("C.Alert", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("C.Percent", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("C.Bar", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("C.BarSize", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("S.Alert", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("S.Percent", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("S.Bar", ImGuiTableColumnFlags.WidthFixed),
                                    UiItem.TableColumn("S.BarSize", ImGuiTableColumnFlags.WidthFixed),
                                ],
                                () => BuildPositionProfileDebugRows(
                                    Conf.PositionProfiles.Values,
                                    Conf.BarConditionSize,
                                    Conf.BarSpiritbondSize))),
                        UiItem.Section("Player Summary", ImGuiTreeNodeFlags.DefaultOpen,
                            UiItem.DynamicText(() => $"Loaded: {PlayerState.IsLoaded}"),
                            UiItem.DynamicText(() => $"Character: {PlayerInfo.GetPlayerDisplayName(PlayerState)}"),
                            UiItem.DynamicText(() => $"Class/Job: {PlayerInfo.GetClassJobFullName(PlayerState, _const.ClassJobs)} ({PlayerInfo.GetClassJobAbbreviation(PlayerState)})"),
                            UiItem.DynamicText(() => $"Class/Job ID: {PlayerState.ClassJob.RowId}"),
                            UiItem.DynamicText(() => $"Level: {PlayerState.Level}"),
                            UiItem.DynamicText(() => $"Effective Level: {PlayerState.EffectiveLevel}"),
                            UiItem.DynamicText(() => $"Level Synced: {PlayerState.IsLevelSynced}"),
                            UiItem.DynamicText(() => $"Entity ID: {PlayerState.EntityId}"),
                            UiItem.DynamicText(() => $"Content ID: {PlayerState.ContentId}")),
                        UiItem.Section("System State", ImGuiTreeNodeFlags.None,
                            UiItem.DynamicText(() => $"Resolution: {_const._currentResolutionId}"),
                            UiItem.DynamicText(() => $"Active: {tracker.IsActive}"),
                            UiItem.DynamicText(() => $"PvP: {tracker.IsInPvPArea}"),
                            UiItem.DynamicText(() => $"UI Hidden: {(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) ? "No" : "Unknown")}"),
                            UiItem.DynamicText(() => $"Testing Mode: {_const._testingMode}")),
                        UiItem.Section("Scanner Health", ImGuiTreeNodeFlags.None,
                            UiItem.DynamicText(() => DebugLine(s => $"Equipment Count: {s.EquippedSlotCount}")),
                            UiItem.DynamicText(() => $"Equipment Updates: {tracker.EquipmentVersion}"),
                            UiItem.DynamicText(() => DebugLine(s => $"Lowest Condition: {s.LowestCondition}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Highest Condition: {s.HighestCondition}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Lowest Spiritbond: {s.LowestSpiritbond}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Highest Spiritbond: {s.HighestSpiritbond}"))),
                        UiItem.Section("Threshold Config", ImGuiTreeNodeFlags.None,
                            UiItem.DynamicText(() => $"Low Threshold: {Conf.ThresholdConditionLow}"),
                            UiItem.DynamicText(() => $"Critical Threshold: {Conf.ThresholdConditionCritical}"),
                            UiItem.DynamicText(() => $"Alert Condition: {(Conf.AlertConditionLowEnabled || Conf.AlertConditionCriticalEnabled || Conf.AlertConditionRepairableEnabled ? "Enabled" : "Disabled")}"),
                            UiItem.DynamicText(() => $"Bar Overlay: {(Conf.BarConditionEnabled ? "Enabled" : "Disabled")}"),
                            UiItem.DynamicText(() => $"Text Overlay: {(Conf.ConditionLabelEnabled ? "Enabled" : "Disabled")}")),
                        UiItem.Section("Equipment Summary", ImGuiTreeNodeFlags.None,
                            UiItem.DynamicText(() => DebugLine(s => $"Expected Total: {s.Summary.ExpectedTotalSlots} ({PlayerInfo.GetClassJobAbbreviation(PlayerState)})")),
                            UiItem.DynamicText(() => DebugLine(s => $"Equipped: {s.Summary.Equipped}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Empty: {s.Summary.Empty}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Needing Repair: {s.Summary.NeedingRepair}")),
                            UiItem.DynamicText(() => DebugLine(s => $"Ready for Extraction: {s.Summary.ReadyForExtraction}"))),
                        UiItem.Section("DTR Bar State", ImGuiTreeNodeFlags.None,
                            UiItem.DynamicText(() => $"DTR Visible: {_const._dtr.HasEntries}"),
                            UiItem.DynamicText(() => $"Condition DTR: {_const._dtr.LastConditionText ?? "(null)"}"),
                            UiItem.DynamicText(() => $"Spiritbond DTR: {_const._dtr.LastSpiritbondText ?? "(null)"}")))
                ],
            };
        }

    }
}
