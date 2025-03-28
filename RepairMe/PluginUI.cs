﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel;

namespace RepairMe
{
    internal class PluginUi : IDisposable
    {
        // orientation combo
        private static readonly string[] OrientationLabels =
        {
            "Left to right", "Right to left", "Top to bottom", "Bottom to top",
        };

        private static readonly int OrientationSize = OrientationLabels.Length;
        private float longestOrientationLabel;

        // position profile combo
        private int selectedProfileCombo = -1;

        // constants
        private const int ProgressLabelPadding = 5;
        private const int TestingModeCycleDurationInt = 15;
        private const float TestingModeCycleDurationFloat = TestingModeCycleDurationInt;
        private readonly float[] EmptyPointsArray = Array.Empty<float>();

        // constants migrated from config
        private readonly string BarConditionWindow = "repairme-bar-condition";
        private readonly string BarSpiritbondWindow = "repairme-bar-spiritbond";
        private readonly string PercentConditionWindow = "repairme-percent-condition";
        private readonly string PercentConditionWindowChild = "repairme-percent-condition-child";
        private readonly string PercentSpiritbondWindow = "repairme-percent-spiritbond";
        private readonly string PercentSpiritbondWindowChild = "repairme-percent-spiritbond-child";
        private readonly string AlertConditionLowWindow = "repairme-alert-condition-low";
        private readonly string AlertConditionLowWindowChild = "repairme-alert-condition-low-child";
        private readonly string AlertConditionCriticalWindow = "repairme-alert-condition-critical";
        private readonly string AlertConditionCriticalWindowChild = "repairme-alert-condition-critical-child";
        private readonly string AlertSpiritbondFullWindow = "repairme-alert-spiritbond-full";
        private readonly string AlertSpiritbondFullWindowChild = "repairme-alert-spiritbond-full-child";
        private readonly uint AlertMessageMaximumLength = 1000;
        private readonly int ThresholdSpiritbondFull = 100;

        // reference fields
        private Configuration conf => Configuration.GetOrLoad();
        private PositionProfile position;
        private PositionProfile? positionUndo = null;
        private readonly EventHandler eventHandler;

        // non-config ui fields
        private bool UnlockedUiModeCheckbox = false;
        private bool UnlockedUiMode => UnlockedUiModeCheckbox && SettingsVisible;
        private float condition = 100;
        private float spiritbond = 0;
        private float leastSpiritbond = 0;
        private float[] spiritbondPoints = Array.Empty<float>();
        private bool testingMode = true;
        private bool isDragging = false;
        private bool isDrawingFirstFrame = true;

        private ExcelSheet<Lumina.Excel.Sheets.Item> items =
            RepairMe.GameData.GetExcelSheet<Lumina.Excel.Sheets.Item>()!;

        private ExcelSheet<Lumina.Excel.Sheets.GeneralAction> generalActions =
            RepairMe.GameData.GetExcelSheet<Lumina.Excel.Sheets.GeneralAction>()!;

        private const uint GeneralActionIdRepair = 6;
        private const uint GeneralActionIdMateriaExtraction = 14;

        public PluginUi(EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        private bool settingsVisible;
        private bool debugVisible = false;

        public void Dispose()
        {
        }

        public void Draw()
        {
            try
            {
                if (!eventHandler.IsActive) return;

                int resolutionWidth = (int)Math.Floor(ImGui.GetMainViewport().Size.X);
                int resolutionHeight = (int)Math.Floor(ImGui.GetMainViewport().Size.Y);
                string resolutionId = $"{resolutionWidth}x{resolutionHeight}";

                if (conf.PositionProfiles.GetValueOrDefault(resolutionId) is not PositionProfile positionProfile)
                {
                    positionProfile = new PositionProfile
                    {
                        ResolutionWidth = resolutionWidth,
                        ResolutionHeight = resolutionHeight
                    };
                    conf.PositionProfiles.Add(positionProfile.Id, positionProfile);
                    conf.Save();
                }

                position = positionProfile;

                if (SettingsVisible && testingMode)
                {
                    condition = (TestingModeCycleDurationInt - DateTime.Now.Second % TestingModeCycleDurationInt) /
                        TestingModeCycleDurationFloat * 100;
                    spiritbond = (DateTime.Now.Second % TestingModeCycleDurationInt + 1) /
                        TestingModeCycleDurationFloat * 100;
                    leastSpiritbond = spiritbond * 0.3f;
                    spiritbondPoints = new[] { 0f, 0.2f, 0.25f, 0.4f, 0.5f, 0f, 0.7f, 0.8f, 0.81f, 0.82f, 0.83f, 0.9f };
                }
                else
                {
                    var e = eventHandler.EquipmentScannerLastEquipmentData;
                    condition = e?.LowestConditionPercent ?? 100f;
                    spiritbond = e?.HighestSpiritbondPercent ?? 0f;
                    leastSpiritbond = e?.LowestSpiritbondPercent ?? 0f;
                    spiritbondPoints = e?.SpiritbondPercents ?? EmptyPointsArray;
                }

                longestOrientationLabel = OrientationLabels.Select(label => ImGui.CalcTextSize(label).X).Max() * 1.35f;

                bool altCharacter = conf.AltCharacters.ContainsKey(RepairMe.ClientState.LocalContentId);

                // bar condition
                DrawConditionBar();

                // bar spiritbond
                DrawSpiritbondBar();

                // percent condition
                DrawPercent(conf.PercentConditionEnabled, condition, PercentConditionWindow,
                    PercentConditionWindowChild, conf.PercentConditionColor, conf.PercentConditionBg,
                    ref position.PercentCondition, conf.PercentConditionShowPercent, conf.PercentConditionShowDecimals);

                // percent spiritbond
                DrawPercent(conf.PercentSpiritbondEnabled, spiritbond, PercentSpiritbondWindow,
                    PercentSpiritbondWindowChild, conf.PercentSpiritbondColor, conf.PercentSpiritbondBg,
                    ref position.PercentSpiritbond, conf.PercentSpiritbondShowPercent,
                    conf.PercentSpiritbondShowDecimals, conf.PercentSpiritbondShowMinMax ? leastSpiritbond : null);

                // alert condition critical
                if ((isDrawingFirstFrame && !conf.PositionsMigrated)
                    || UnlockedUiMode
                    || altCharacter && condition <= conf.ThresholdConditionCriticalAlt
                    || !altCharacter && condition <= conf.ThresholdConditionCritical)
                    DrawAlert(conf.AlertConditionCriticalEnabled, conf.AlertConditionCriticalText,
                        AlertConditionCriticalWindow, AlertConditionCriticalWindowChild,
                        conf.AlertConditionCriticalColor, conf.AlertConditionCriticalBg,
                        ref position.AlertCriticalCondition,
                        conf.AlertConditionCriticalShortcut ? ClickActionOpenRepairs : null);

                // alert condition low
                if ((isDrawingFirstFrame && !conf.PositionsMigrated)
                    || UnlockedUiMode
                    || altCharacter
                    && condition <= conf.ThresholdConditionLowAlt
                    && condition > conf.ThresholdConditionCriticalAlt
                    || !altCharacter
                    && condition <= conf.ThresholdConditionLow
                    && condition > conf.ThresholdConditionCritical
                   )
                    DrawAlert(conf.AlertConditionLowEnabled, conf.AlertConditionLowText, AlertConditionLowWindow,
                        AlertConditionLowWindowChild, conf.AlertConditionLowColor, conf.AlertConditionLowBg,
                        ref position.AlertLowCondition, conf.AlertConditionLowShortcut ? ClickActionOpenRepairs : null);

                // alert spiritbond full
                if ((isDrawingFirstFrame && !conf.PositionsMigrated)
                    || UnlockedUiMode
                    || ThresholdSpiritbondFull <= spiritbond)
                    DrawAlert(conf.AlertSpiritbondFullEnabled, conf.AlertSpiritbondFullText, AlertSpiritbondFullWindow,
                        AlertSpiritbondFullWindowChild, conf.AlertSpiritbondFullColor, conf.AlertSpiritbondFullBg,
                        ref position.AlertSpiritbond,
                        conf.AlertSpiritbondShortcut ? ClickActionOpenMateriaExtraction : null);

                DrawSettingsWindow();
                DrawDebugWindow();

                if (isDrawingFirstFrame && !conf.PositionsMigrated)
                {
                    conf.PositionsMigrated = true;
                    conf.Save();
                }

                isDrawingFirstFrame = false;
            }
            catch (Exception ex)
            {
                RepairMe.Log.Error(ex, "prevented GUI crash");
            }
        }

        private unsafe void ClickActionOpenRepairs()
        {
            try
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, GeneralActionIdRepair);
            }
            catch (Exception e)
            {
                RepairMe.Log.Error("Could not use general action repair", e);
            }
        }

        private unsafe void ClickActionOpenMateriaExtraction()
        {
            try
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, GeneralActionIdMateriaExtraction);
            }
            catch (Exception e)
            {
                RepairMe.Log.Error("Could not use general action materia extraction", e);
            }
        }

        private void CheckDrag(ref Vector2 pos)
        {
            // https://github.com/UnknownX7/QoLBar/blob/f73515daf812058d0af58cdbd0942249474e9cf8/UI/BarUI.cs

            if (isDrawingFirstFrame) return;

            var dragging = !isDragging
                ? ImGui.IsWindowFocused() && ImGui.IsWindowHovered() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                  ImGui.IsMouseDragging(ImGuiMouseButton.Left, 5)
                : isDragging && !ImGui.IsMouseReleased(ImGuiMouseButton.Left);

            // Began dragging
            if (dragging && dragging != isDragging)
            {
                isDragging = true;
            }

            // is dragging current window
            if (isDragging && ImGui.IsWindowFocused())
            {
                var vp = ImGuiHelpers.MainViewport.Size;

                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0);
                ImGui.ResetMouseDragDelta();
                pos.X += delta.X;
                pos.Y += delta.Y;
            }

            // Stopped dragging
            if (!dragging && dragging != isDragging)
            {
                isDragging = false;
                conf.Save();
            }
        }

        private ImGuiWindowFlags PrepareWindow(Vector2 pos)
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                         ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

            ImGuiHelpers.ForceNextWindowMainViewport();

            if (conf.PositionsMigrated && !isDragging)
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + pos);

            if (UnlockedUiMode) return wFlags;

            wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

            return wFlags;
        }

        private void PushWindowEditingStyle(bool minSize = false)
        {
            if (minSize)
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0, ImGui.GetFontSize()));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
        }

        private void PopWindowEditingStyle(bool minSize = false)
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(minSize ? 2 : 1);
        }

        private void PushBarColors(Vector4 color, Vector4 bg)
        {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, bg);
        }

        private void PopBarColors()
        {
            ImGui.PopStyleColor(2);
        }

        private void DrawConditionBar()
        {
            if (conf.PositionsMigrated && !conf.BarConditionEnabled) return;

            var windowFlags = PrepareWindow(position.BarCondition);

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarConditionSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(BarConditionWindow, windowFlags))
            {
                MigratePositions(ref position.BarCondition);
                CheckDrag(ref position.BarCondition);

                bool altCharacter = conf.AltCharacters.ContainsKey(RepairMe.ClientState.LocalContentId);

                if (altCharacter && condition <= conf.ThresholdConditionCriticalAlt
                    || !altCharacter && condition <= conf.ThresholdConditionCritical)
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionCriticalColor,
                        conf.BarConditionCriticalBackground,
                        false,
                        EmptyPointsArray,
                        conf.BarConditionCriticalColor,
                        conf.BarConditionRounding,
                        conf.BarConditionBorderSize,
                        conf.BarConditionBorderColor
                    );
                else if (altCharacter && condition <= conf.ThresholdConditionLowAlt
                         || !altCharacter && condition <= conf.ThresholdConditionLow)
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionLowColor,
                        conf.BarConditionLowBackground,
                        false,
                        EmptyPointsArray,
                        conf.BarConditionLowColor,
                        conf.BarConditionRounding,
                        conf.BarConditionBorderSize,
                        conf.BarConditionBorderColor
                    );
                else
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionOkColor,
                        conf.BarConditionOkBackground,
                        false,
                        EmptyPointsArray,
                        conf.BarConditionOkColor,
                        conf.BarConditionRounding,
                        conf.BarConditionBorderSize,
                        conf.BarConditionBorderColor
                    );
            }

            ImGui.End();
            PopWindowEditingStyle();
        }

        private void ProgressBar(float progress, int orientation, Vector2 size, Vector4 fgColor, Vector4 bgColor,
            bool showPoints, float[] points, Vector4 pointsColor, float rounding, float borderSize, Vector4 borderColor)
        {
            // have border grow only inwards instead of outwards+inwards
            float innerOffset = borderSize;
            float outterOffset = borderSize % 2 == 0 ? borderSize / 2f - 0.5f : (borderSize - 1) / 2f;
            var pointTopLeftOutter = ImGui.GetCursorScreenPos() + Vector2.One * outterOffset;
            var sizeOutter = size - Vector2.One * outterOffset * 2;
            var pointTopLeft = ImGui.GetCursorScreenPos() + Vector2.One * innerOffset;
            size -= Vector2.One * innerOffset * 2;

            var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());

            if (borderSize > 0)
                bdl.AddRect(pointTopLeftOutter,
                    new Vector2(pointTopLeftOutter.X + sizeOutter.X, pointTopLeftOutter.Y + sizeOutter.Y),
                    ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.Closed, borderSize);

            bdl.AddRectFilled(
                pointTopLeft,
                new Vector2(pointTopLeft.X + size.X, pointTopLeft.Y + size.Y),
                ImGui.GetColorU32(bgColor),
                rounding);

            switch (orientation)
            {
                default: // also case 0: left to right
                    bdl.PushClipRect(
                        pointTopLeft,
                        new Vector2(
                            pointTopLeft.X + size.X * progress,
                            pointTopLeft.Y + size.Y
                        )
                    );
                    break;
                case 1: // right to left
                    bdl.PushClipRect(
                        new Vector2(
                            pointTopLeft.X + size.X * (1 - progress),
                            pointTopLeft.Y
                        ),
                        new Vector2(
                            pointTopLeft.X + size.X,
                            pointTopLeft.Y + size.Y
                        )
                    );
                    break;
                case 2: // top to bottom
                    bdl.PushClipRect(
                        pointTopLeft,
                        new Vector2(
                            pointTopLeft.X + size.X,
                            pointTopLeft.Y + size.Y * progress
                        )
                    );
                    break;
                case 3: // bottom to top
                    bdl.PushClipRect(
                        new Vector2(
                            pointTopLeft.X,
                            pointTopLeft.Y + size.Y * (1 - progress)
                        ),
                        new Vector2(
                            pointTopLeft.X + size.X,
                            pointTopLeft.Y + size.Y
                        )
                    );
                    break;
            }

            bdl.AddRectFilled(
                pointTopLeft,
                new Vector2(pointTopLeft.X + size.X, pointTopLeft.Y + size.Y),
                ImGui.GetColorU32(fgColor),
                rounding);
            bdl.PopClipRect();

            if (!showPoints) return;

            float pointSize = 1;
            for (int i = 0; i < points.Length; i++)
            {
                if (progress <= points[i] || points[i] < 0)
                    continue;

                switch (orientation)
                {
                    default: // also case 0: left to right

                        bdl.PushClipRect(
                            new Vector2(
                                pointTopLeft.X + size.X * points[i],
                                pointTopLeft.Y
                            ),
                            new Vector2(
                                pointTopLeft.X + size.X * points[i] + pointSize,
                                pointTopLeft.Y + size.Y
                            )
                        );

                        break;
                    case 1: // right to left
                        bdl.PushClipRect(
                            new Vector2(
                                pointTopLeft.X + size.X * (1 - points[i]),
                                pointTopLeft.Y
                            ),
                            new Vector2(
                                pointTopLeft.X + size.X * (1 - points[i]) + pointSize,
                                pointTopLeft.Y + size.Y
                            )
                        );
                        break;
                    case 2: // top to bottom
                        bdl.PushClipRect(
                            new Vector2(
                                pointTopLeft.X,
                                pointTopLeft.Y + size.Y * points[i]
                            ),
                            new Vector2(
                                pointTopLeft.X + size.X,
                                pointTopLeft.Y + size.Y * points[i] + pointSize
                            )
                        );
                        break;
                    case 3: // bottom to top
                        bdl.PushClipRect(
                            new Vector2(
                                pointTopLeft.X,
                                pointTopLeft.Y + size.Y * (1 - points[i])
                            ),
                            new Vector2(
                                pointTopLeft.X + size.X,
                                pointTopLeft.Y + size.Y * (1 - points[i]) + pointSize
                            )
                        );

                        break;
                }

                bdl.AddRectFilled(
                    pointTopLeft,
                    new Vector2(pointTopLeft.X + size.X, pointTopLeft.Y + size.Y),
                    ImGui.GetColorU32(pointsColor),
                    rounding);
                bdl.PopClipRect();
            }
        }

        private void DrawSpiritbondBar()
        {
            if (conf.PositionsMigrated && !conf.BarSpiritbondEnabled) return;

            var windowFlags = PrepareWindow(position.BarSpiritbond);

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarSpiritbondSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(BarSpiritbondWindow, windowFlags))
            {
                MigratePositions(ref position.BarSpiritbond);
                CheckDrag(ref position.BarSpiritbond);

                if (spiritbond < ThresholdSpiritbondFull)
                    ProgressBar(spiritbond / 100f,
                        conf.BarSpiritbondOrientation,
                        conf.BarSpiritbondSize,
                        conf.BarSpiritbondProgressColor,
                        conf.BarSpiritbondProgressBackground,
                        conf.BarSpiritbondShowAllItems,
                        spiritbondPoints,
                        conf.BarSpiritbondPointsColor,
                        conf.BarSpiritbondRounding,
                        conf.BarSpiritbondBorderSize,
                        conf.BarSpiritbondBorderColor
                    );
                else
                    ProgressBar(spiritbond / 100f,
                        conf.BarSpiritbondOrientation,
                        conf.BarSpiritbondSize,
                        conf.BarSpiritbondFullColor,
                        conf.BarSpiritbondFullBackground,
                        conf.BarSpiritbondShowAllItems,
                        spiritbondPoints,
                        conf.BarSpiritbondPointsColor,
                        conf.BarSpiritbondRounding,
                        conf.BarSpiritbondBorderSize,
                        conf.BarSpiritbondBorderColor
                    );
            }

            ImGui.End();
            PopWindowEditingStyle();
        }

        private void DrawPercent(bool percentEnabled, float percent, string percentWindow, string percentWindowChild,
            Vector4 percentColor, Vector4 percentBg, ref Vector2 percentPosition, bool showSign, bool showDecimals,
            float? extraValue = null)
        {
            string text =
                showSign && showDecimals ? $"{percent:F2}%"
                : showSign ? $"{Math.Floor(percent):F0}%"
                : showDecimals ? $"{percent:F2}"
                : $"{Math.Floor(percent):F0}";

            if (extraValue != null)
                text =
                    (showSign && showDecimals ? $"{extraValue.Value:F2}%"
                        : showSign ? $"{Math.Floor(extraValue.Value):F0}%"
                        : showDecimals ? $"{extraValue.Value:F2}"
                        : $"{Math.Floor(extraValue.Value):F0}") + " / " + text;

            DrawText(percentEnabled, text, false, percentWindow, percentWindowChild,
                percentColor, percentBg, ref percentPosition, null);
        }

        private void DrawAlert(bool alertEnabled, string text, string alertWindow, string alertWindowChild,
            Vector4 alertColor, Vector4 alertBg, ref Vector2 alertPosition, Action? clickAction)
        {
            DrawText(alertEnabled, text, true, alertWindow, alertWindowChild,
                alertColor, alertBg, ref alertPosition, clickAction);
        }

        private void DrawText(bool enabled, string text, bool isAlert, string window, string windowChild, Vector4 color,
            Vector4 bg, ref Vector2 position, Action? clickAction)
        {
            if (conf.PositionsMigrated && !enabled) return;

            var windowFlags = PrepareWindow(position);
            PushWindowEditingStyle(isAlert);
            if (ImGui.Begin(window, windowFlags))
            {
                MigratePositions(ref position);
                CheckDrag(ref position);

                var childSize = ImGui.CalcTextSize(text) + Vector2.One * 2;
                childSize.X += ProgressLabelPadding * 2;

                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5f);
                ImGui.PushStyleColor(ImGuiCol.ChildBg, bg);

                if (ImGui.BeginChild(windowChild, childSize, false,
                        clickAction != null ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs))
                {
                    var pos = ImGui.GetCursorPos();
                    pos.X += ProgressLabelPadding;

                    bool hovering = !UnlockedUiMode && clickAction != null && ImGui.IsWindowHovered();
                    bool clicking = hovering && ImGui.IsMouseDown(ImGuiMouseButton.Left);
                    bool clicked = hovering && ImGui.IsMouseReleased(ImGuiMouseButton.Left);

                    if (hovering)
                    {
                        ImGui.SetCursorPos(pos + Vector2.One * (clicking || clicked ? -1 : 1));
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, color.W));
                        ImGui.TextUnformatted(text);
                        ImGui.PopStyleColor();
                    }

                    ImGui.SetCursorPos(pos);
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    ImGui.TextUnformatted(text);
                    ImGui.PopStyleColor();

                    if (!UnlockedUiMode && clickAction != null && ImGui.IsWindowHovered() &&
                        ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                        clickAction.Invoke();

                    ImGui.EndChild();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
            }

            ImGui.End();

            PopWindowEditingStyle(isAlert);
        }

        private void ColorPicker(string label, ref Vector4 color)
        {
            ImGui.TableNextColumn();
            if (ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.NoInputs))
                conf.Save();
        }

        private void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;

            ImGui.SetNextWindowSize(new Vector2(510, 525), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("RepairMe config", ref settingsVisible))
            {
                ImGui.End();
                return;
            }

            if (ImGui.Checkbox("Move UI##repairMe001", ref UnlockedUiModeCheckbox)) conf.Save();

            ImGui.SameLine();
            if (ImGui.Checkbox("Testing mode##repairMe002", ref testingMode)) conf.Save();

            ImGui.SameLine();
            if (ImGui.Checkbox("Hide when player is occupied##repairMe002.1", ref conf.HideUiWhenOccupied)) conf.Save();

            if (RepairMe.Keys[VirtualKey.SHIFT])
            {
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 12);
                if (ImGuiEx.IconButton(FontAwesomeIcon.Bug, "Debug info"))
                    debugVisible = !debugVisible;
            }

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Condition settings##repairMe003", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Checkbox("Show Bar##repairMe004", ref conf.BarConditionEnabled)) conf.Save();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(longestOrientationLabel);
                if (ImGui.Combo("Orientation##repairMe004.2", ref conf.BarConditionOrientation,
                        OrientationLabels, OrientationSize))
                    conf.Save();

                if (ImGui.Checkbox("Show Percentage##repairMe005", ref conf.PercentConditionEnabled))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Show decimals##repairMe005.1", ref conf.PercentConditionShowDecimals))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Show % sign##repairMe005.2", ref conf.PercentConditionShowPercent))
                    conf.Save();

                if (ImGui.Checkbox("Show Alert when Low##repairMe006", ref conf.AlertConditionLowEnabled))
                    conf.Save();

                ImGui.SameLine();
                if (ImGui.Checkbox("Clicking alert opens Repairs window##repairMe006.1",
                        ref conf.AlertConditionLowShortcut))
                    conf.Save();

                if (ImGui.Checkbox("Show Alert when Critical##repairMe007", ref conf.AlertConditionCriticalEnabled))
                    conf.Save();

                ImGui.SameLine();
                if (ImGui.Checkbox("Clicking alert opens Repairs window##repairMe007.1",
                        ref conf.AlertConditionCriticalShortcut))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Positions");

                if (ImGuiCoordinatesInput("##repairMe039", ref position.AlertLowCondition))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe039.1"))
                {
                    position.AlertLowCondition = new PositionProfile().AlertLowCondition;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Alert Low position");


                if (ImGuiCoordinatesInput("##repairMe039.2", ref position.AlertCriticalCondition))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe039.3"))
                {
                    position.AlertCriticalCondition = new PositionProfile().AlertCriticalCondition;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Alert Critical position");

                if (ImGuiCoordinatesInput("##repairMe040", ref position.PercentCondition))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe040.1"))
                {
                    position.PercentCondition = new PositionProfile().PercentCondition;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Percentage position");

                if (ImGuiCoordinatesInput("##repairMe041", ref position.BarCondition))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe041.1"))
                {
                    position.BarCondition = new PositionProfile().BarCondition;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Bar position");

                ImGui.Spacing();
                ImGui.Text("Size");

                if (ImGuiCoordinatesInput("Bar size##repairMe008", ref conf.BarConditionSize))
                    conf.Save();
                ImGui.PushItemWidth(80);
                if (ImGui.DragFloat("Bar rounding##repairMe008.1", ref conf.BarConditionRounding, 1f, 0, float.MaxValue,
                        "%.0f"))
                    conf.Save();
                if (ImGui.DragFloat("Border size##repairMe008.2", ref conf.BarConditionBorderSize, 1f, 0,
                        float.MaxValue,
                        "%.0f"))
                    conf.Save();
                ImGui.PopItemWidth();

                ImGui.Spacing();
                ImGui.Text("Threshold - Order is OK > Low > Critical. This affects the condition bar and alerts");
                if (ImGui.SliderInt("Low threshold##repairMe009", ref conf.ThresholdConditionLow, 0, 100, "%d%%"))
                {
                    if (conf.ThresholdConditionLow <= conf.ThresholdConditionCritical)
                        conf.ThresholdConditionLow = conf.ThresholdConditionCritical + 1;
                    conf.Save();
                }

                if (ImGui.SliderInt("Critical threshold##repairMe010", ref conf.ThresholdConditionCritical, 0, 100,
                        "%d%%"))
                {
                    if (conf.ThresholdConditionCritical >= conf.ThresholdConditionLow)
                        conf.ThresholdConditionCritical = conf.ThresholdConditionLow - 1;
                    conf.Save();
                }

                ImGui.Spacing();
                ImGui.Text("Alert messages");

                if (ImGui.InputTextWithHint("Alert Low Message##repairMe011", "", ref conf.AlertConditionLowText,
                        AlertMessageMaximumLength)) conf.Save();
                if (ImGui.InputTextWithHint("Alert Critical Message##repairMe012", "",
                        ref conf.AlertConditionCriticalText,
                        AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();
                ImGui.Text("Colors - They support transparency, including becoming fully transparent");
                if (ImGui.BeginTable("condition-colors##repairMe013", 2))
                {
                    ColorPicker("Percentage Color##repairMe014", ref conf.PercentConditionColor);
                    ColorPicker("Percentage Background##repairMe015", ref conf.PercentConditionBg);
                    ColorPicker("Bar Color##repairMe016", ref conf.BarConditionOkColor);
                    ColorPicker("Bar Background##repairMe017", ref conf.BarConditionOkBackground);
                    ColorPicker("Bar Low Color##repairMe018", ref conf.BarConditionLowColor);
                    ColorPicker("Bar Low Background##repairMe019", ref conf.BarConditionLowBackground);
                    ColorPicker("Bar Critical Color##repairMe020", ref conf.BarConditionCriticalColor);
                    ColorPicker("Bar Critical Background##repairMe021", ref conf.BarConditionCriticalBackground);
                    ColorPicker("Bar Border Color##repairMe021.1", ref conf.BarConditionBorderColor);
                    ImGui.TableNextColumn();
                    ColorPicker("Alert Low Color##repairMe022", ref conf.AlertConditionLowColor);
                    ColorPicker("Alert Low Background##repairMe023", ref conf.AlertConditionLowBg);
                    ColorPicker("Alert Critical Color##repairMe024", ref conf.AlertConditionCriticalColor);
                    ColorPicker("Alert Critical Background##repairMe025", ref conf.AlertConditionCriticalBg);
                    ImGui.EndTable();
                }

                ImGui.Spacing();
            }

            if (ImGui.CollapsingHeader("Alternative condition settings##repairMe050"))
            {
                ImGui.Text("Configure alternative alert thresholds for specific characters");

                ImGui.Spacing();
                ImGui.Text("Alternative thresholds");
                if (ImGui.SliderInt("Low threshold##repairMe052", ref conf.ThresholdConditionLowAlt, 0, 100, "%d%%"))
                {
                    if (conf.ThresholdConditionLowAlt <= conf.ThresholdConditionCriticalAlt)
                        conf.ThresholdConditionLowAlt = conf.ThresholdConditionCriticalAlt + 1;
                    conf.Save();
                }

                if (ImGui.SliderInt("Critical threshold##repairMe053", ref conf.ThresholdConditionCriticalAlt, 0, 100,
                        "%d%%"))
                {
                    if (conf.ThresholdConditionCriticalAlt >= conf.ThresholdConditionLowAlt)
                        conf.ThresholdConditionCriticalAlt = conf.ThresholdConditionLowAlt - 1;
                    conf.Save();
                }

                ImGui.Spacing();

                if (conf.AltCharacters.Count > 0 && ImGui.BeginTable("##AltCharsrepairMe051", 3))
                {
                    ImGui.TableSetupColumn("##AltCharsrepairMe051-c1", ImGuiTableColumnFlags.WidthFixed,
                        ImGui.CalcTextSize("8").X + ImGui.GetStyle().CellPadding.X * 2);
#if DEBUG
                    ImGui.TableSetupColumn("ID##AltCharsrepairMe051-c2", ImGuiTableColumnFlags.WidthFixed,
                        ImGui.CalcTextSize("8888888888888888").X+ImGui.GetStyle().CellPadding.X*2);
#endif
                    ImGui.TableSetupColumn("Character##AltCharsrepairMe051-c3");
                    ImGui.TableHeadersRow();

                    ulong removal = 0;

                    foreach (var altCharacter in conf.AltCharacters)
                    {
                        ImGui.TableNextColumn();
                        if (ImGuiEx.IconButton(FontAwesomeIcon.Times, "Shift+Click to remove from list")
                            && RepairMe.Keys[VirtualKey.SHIFT])
                            removal = altCharacter.Key;
#if DEBUG
                        ImGui.TableNextColumn();
                        ImGui.Text($"{altCharacter.Key:X16}");
#endif
                        ImGui.TableNextColumn();
                        ImGui.Text(altCharacter.Value);
                    }

                    ImGui.EndTable();

                    if (removal != 0 && conf.AltCharacters.ContainsKey(removal))
                    {
                        conf.AltCharacters.Remove(removal);
                        conf.Save();
                    }

                    ImGui.Spacing();
                }

                if (ImGui.Button("Apply to current character##AltCharsrepairMe051") && RepairMe.ClientState.LocalPlayer != null)
                {
                    conf.AltCharacters[RepairMe.ClientState.LocalContentId] = RepairMe.ClientState.LocalPlayer!.Name + " @ " +
                                                                              RepairMe.ClientState.LocalPlayer!.HomeWorld.Value;
                    conf.Save();
                }

                ImGui.Spacing();
            }

            if (ImGui.CollapsingHeader("Spiritbond settings##repairMe060"))
            {
                if (ImGui.Checkbox("Show Bar##repairMe026", ref conf.BarSpiritbondEnabled))
                    conf.Save();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(longestOrientationLabel);
                if (ImGui.Combo("Orientation##repairMe026.2", ref conf.BarSpiritbondOrientation,
                        OrientationLabels, OrientationSize))
                    conf.Save();

                ImGui.SameLine();
                if (ImGui.Checkbox("Show all items in bar##repairMe026.3", ref conf.BarSpiritbondShowAllItems))
                    conf.Save();

                if (ImGui.Checkbox("Show Percentage##repairMe027", ref conf.PercentSpiritbondEnabled))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Show decimals##repairMe027.1", ref conf.PercentSpiritbondShowDecimals))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Show % sign##repairMe027.2", ref conf.PercentSpiritbondShowPercent))
                    conf.Save();
                if (ImGui.Checkbox("Also show least spiritbonded item##repairMe005.3",
                        ref conf.PercentSpiritbondShowMinMax))
                    conf.Save();


                if (ImGui.Checkbox("Show Alert when Full##repairMe028", ref conf.AlertSpiritbondFullEnabled))
                    conf.Save();

                ImGui.SameLine();
                if (ImGui.Checkbox("Clicking alert opens Materia Extraction window##repairMe028.1",
                        ref conf.AlertSpiritbondShortcut))
                    conf.Save();


                ImGui.Spacing();
                ImGui.Text("Positions");
                if (ImGuiCoordinatesInput("##repairMe042", ref position.AlertSpiritbond))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe042.1"))
                {
                    position.AlertSpiritbond = new PositionProfile().AlertSpiritbond;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Alert Full position");

                if (ImGuiCoordinatesInput("##repairMe043", ref position.PercentSpiritbond))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe043.1"))
                {
                    position.PercentSpiritbond = new PositionProfile().PercentSpiritbond;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Percentage position");

                if (ImGuiCoordinatesInput("##repairMe044", ref position.BarSpiritbond))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Button("reset##repairMe044.1"))
                {
                    position.BarSpiritbond = new PositionProfile().BarSpiritbond;
                    conf.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Bar position");

                ImGui.Spacing();
                ImGui.Text("Size");
                if (ImGuiCoordinatesInput("Bar size##repairMe029", ref conf.BarSpiritbondSize))
                    conf.Save();
                ImGui.PushItemWidth(80);
                if (ImGui.DragFloat("Bar rounding##repairMe029.1", ref conf.BarSpiritbondRounding, 1f, 0,
                        float.MaxValue, "%.0f"))
                    conf.Save();
                if (ImGui.DragFloat("Border size##repairMe029.2", ref conf.BarSpiritbondBorderSize, 1f, 0,
                        float.MaxValue,
                        "%.0f"))
                    conf.Save();
                ImGui.PopItemWidth();

                ImGui.Spacing();
                ImGui.Text("Alert messages");

                if (ImGui.InputTextWithHint("Alert Full Message##repairMe030", "", ref conf.AlertSpiritbondFullText,
                        AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();

                ImGui.Text("Colors - They support transparency, including becoming fully transparent");
                if (ImGui.BeginTable("spiritbond-colors", 2))
                {
                    ColorPicker("Percentage Color##repairMe031", ref conf.PercentSpiritbondColor);
                    ColorPicker("Percentage Background##repairMe032", ref conf.PercentSpiritbondBg);
                    ColorPicker("Bar Color##repairMe033", ref conf.BarSpiritbondProgressColor);
                    ColorPicker("Bar Background##repairMe034", ref conf.BarSpiritbondProgressBackground);
                    ColorPicker("Bar Full Color##repairMe035", ref conf.BarSpiritbondFullColor);
                    ColorPicker("Bar Full Background##repairMe036", ref conf.BarSpiritbondFullBackground);
                    ColorPicker("Border Color##repairMe036.1", ref conf.BarSpiritbondBorderColor);
                    ColorPicker("Other items color##repairMe039", ref conf.BarSpiritbondPointsColor);
                    ColorPicker("Alert Full Color##repairMe037", ref conf.AlertSpiritbondFullColor);
                    ColorPicker("Alert Full Background##repairMe038", ref conf.AlertSpiritbondFullBg);
                    ImGui.TableNextColumn();
                    ImGui.EndTable();
                }

                ImGui.Spacing();
            }

            if (ImGui.CollapsingHeader("Resolution/positioning settings##repairMe045"))
            {
                ImGui.Text("If you changed you resolution and want to copy position settings from other resolution");

                string[] resolutions = conf.PositionProfiles.Keys.ToArray();
                int current = selectedProfileCombo;
                if (current == -1)
                    for (current = 0; current < resolutions.Length; current++)
                        if (resolutions[current] == position.Id)
                            break;

                if (current == resolutions.Length) current = 0;

                ImGui.Text("Copy position settings from");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.CalcTextSize("9999x9999").X * 1.75f);
                if (ImGui.Combo("##repairMe046", ref current, resolutions, resolutions.Length))
                {
                    selectedProfileCombo = current;
                }

                ImGui.SameLine();
                if (ImGui.Button("Copy"))
                {
                    positionUndo = new PositionProfile();
                    positionUndo.CopyFrom(position);

                    PositionProfile? source =
                        conf.PositionProfiles.GetValueOrDefault(resolutions[selectedProfileCombo]);
                    if (source != null)
                    {
                        position.CopyFrom(source);
                        conf.Save();
                    }
                }

                ImGui.SameLine();

                if (positionUndo != null)
                {
                    if (ImGui.Button("Undo last copy"))
                    {
                        position.CopyFrom(positionUndo);
                        conf.Save();
                        positionUndo = null;
                    }
                }

                ImGui.Spacing();
            }

            ImGui.End();
        }

        private void MigratePositions(ref Vector2 position)
        {
            if (conf.PositionsMigrated || !isDrawingFirstFrame) return;

            var pos = ImGui.GetWindowPos() - ImGui.GetMainViewport().Pos;
            position.X = pos.X;
            position.Y = pos.Y;
        }

        private static bool ImGuiCoordinatesInput(string label,
            ref Vector2 v)
        {
            ImGui.PushItemWidth(80);
            var result = ImGui.DragFloat2(label, ref v, 1f, 1, float.MaxValue, "%.0f");
            ImGui.PopItemWidth();
            return result;
        }

        private void DrawDebugWindow()
        {
            if (!debugVisible || eventHandler.EquipmentScannerLastEquipmentData == null) return;

            var e = eventHandler.EquipmentScannerLastEquipmentData.Value;

            if (ImGui.Begin("RepairMe Debug", ref debugVisible,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Equipment");

                if (ImGui.BeginTable("RepairMe items table", 6, ImGuiTableFlags.BordersInnerH))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("i");
                    ImGui.TableNextColumn();
                    ImGui.Text("slot");
                    ImGui.TableNextColumn();
                    ImGui.Text("id");
                    ImGui.TableNextColumn();
                    ImGui.Text("Cond");
                    ImGui.TableNextColumn();
                    ImGui.Text("Spiritb");
                    ImGui.TableNextColumn();
                    ImGui.Text("Name");

                    for (int i = 0; i < EquipmentScanner.EquipmentContainerSize; i++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(i.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(EquipmentScanner.EquipmentSlotNames[i]);
                        ImGui.TableNextColumn();
                        ImGui.Text(e.Id[i].ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(e.Condition[i] / 300f):F2}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(e.Spiritbond[i] / 100f):F2}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{items.GetRow(e.Id[i]).Name}");
                    }

                    ImGui.EndTable();
                }

                ImGui.Spacing();
                ImGui.Text($"Resolutions & Positions");

                if (ImGui.BeginTable("RepairMe positions table", 10, ImGuiTableFlags.BordersInnerH))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("resolution");
                    ImGui.TableNextColumn();
                    ImGui.Text("C.ALow");
                    ImGui.TableNextColumn();
                    ImGui.Text("C.ACrit");
                    ImGui.TableNextColumn();
                    ImGui.Text("C.Perct");
                    ImGui.TableNextColumn();
                    ImGui.Text("C.Bar");
                    ImGui.TableNextColumn();
                    ImGui.Text("C.BarSize");
                    ImGui.TableNextColumn();
                    ImGui.Text("S.A");
                    ImGui.TableNextColumn();
                    ImGui.Text("S.Perct");
                    ImGui.TableNextColumn();
                    ImGui.Text("S.Bar");
                    ImGui.TableNextColumn();
                    ImGui.Text("S.BarSize");

                    foreach (var p in conf.PositionProfiles.Values)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        if (position.Id == p.Id)
                            ImGui.Text($"» {p.Id} «");
                        else
                        {
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.CalcTextSize(" »").X);
                            ImGui.Text($"{p.Id}");
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.AlertLowCondition.X}x{p.AlertLowCondition.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.AlertCriticalCondition.X}x{p.AlertCriticalCondition.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.PercentCondition.X}x{p.PercentCondition.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.BarCondition.X}x{p.BarCondition.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{conf.BarConditionSize.X}x{conf.BarConditionSize.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.AlertSpiritbond.X}x{p.AlertSpiritbond.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.PercentSpiritbond.X}x{p.PercentSpiritbond.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{p.BarSpiritbond.X}x{p.BarSpiritbond.Y}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{conf.BarSpiritbondSize.X}x{conf.BarSpiritbondSize.Y}");
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.End();
        }
    }
}