using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;

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

        private readonly Vector4 initialBorderColor;

        // reference fields
        private Configuration conf => Configuration.GetOrLoad();
        private PositionProfile position;
        private PositionProfile? positionUndo = null;
        private readonly EventHandler eventHandler;

        // non-config ui fields
        private bool highlightCheckbox = false;
        private bool highlightMode => highlightCheckbox && SettingsVisible;
        private float condition = 100;
        private float spiritbond = 0;
        private bool testingMode = true;

        public PluginUi(EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
            initialBorderColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
        }

        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        private bool settingsVisible;

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
                }
                else
                {
                    var e = eventHandler.EquipmentScannerLastEquipmentData;
                    condition = e?.LowestConditionPercent ?? 100f;
                    spiritbond = e?.HighestSpiritbondPercent ?? 0f;
                }

                longestOrientationLabel = OrientationLabels.Select(label => ImGui.CalcTextSize(label).X).Max() * 1.35f;

                // bar condition
                DrawConditionBar();

                // bar spiritbond
                DrawSpiritbondBar();

                // percent condition
                DrawPercent(conf.PercentConditionEnabled, condition, PercentConditionWindow,
                    PercentConditionWindowChild, conf.PercentConditionColor, conf.PercentConditionBg,
                    position.PercentCondition);

                // percent spiritbond
                DrawPercent(conf.PercentSpiritbondEnabled, spiritbond, PercentSpiritbondWindow,
                    PercentSpiritbondWindowChild, conf.PercentSpiritbondColor, conf.PercentSpiritbondBg,
                    position.PercentSpiritbond);

                // alert condition critical
                if (highlightMode || condition <= conf.ThresholdConditionCritical)
                    DrawAlert(conf.AlertConditionCriticalEnabled, conf.AlertConditionCriticalText,
                        AlertConditionCriticalWindow, AlertConditionCriticalWindowChild,
                        conf.AlertConditionCriticalColor, conf.AlertConditionCriticalBg, position.AlertCondition);

                // alert condition low
                if (highlightMode || condition <= conf.ThresholdConditionLow &&
                    condition > conf.ThresholdConditionCritical)
                    DrawAlert(conf.AlertConditionLowEnabled, conf.AlertConditionLowText, AlertConditionLowWindow,
                        AlertConditionLowWindowChild, conf.AlertConditionLowColor, conf.AlertConditionLowBg,
                        position.AlertCondition);

                // alert spiritbond full
                if (highlightMode || ThresholdSpiritbondFull <= spiritbond)
                    DrawAlert(conf.AlertSpiritbondFullEnabled, conf.AlertSpiritbondFullText,
                        AlertSpiritbondFullWindow, AlertSpiritbondFullWindowChild,
                        conf.AlertSpiritbondFullColor, conf.AlertSpiritbondFullBg, position.AlertSpiritbond);

                DrawSettingsWindow();

#if DEBUG
                DrawDebugWindow();
#endif
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "prevented GUI crash");
            }
        }


        private ImGuiWindowFlags PrepareWindow(Vector2 position)
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                         ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking |
                         ImGuiWindowFlags.NoInputs;

            ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + position);

            if (highlightMode) return wFlags;

            wFlags |= ImGuiWindowFlags.NoBackground;
            //ImGui.SetWindowPos(windowName, windowPos);

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
            if (!conf.BarConditionEnabled) return;

            var windowFlags = PrepareWindow(position.BarCondition);

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarConditionSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(BarConditionWindow, windowFlags))
            {
                if (condition <= conf.ThresholdConditionCritical)
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionCriticalColor,
                        conf.BarConditionCriticalBackground
                    );
                else if (condition <= conf.ThresholdConditionLow)
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionLowColor,
                        conf.BarConditionLowBackground
                    );
                else
                    ProgressBar(condition / 100f,
                        conf.BarConditionOrientation,
                        conf.BarConditionSize,
                        conf.BarConditionOkColor,
                        conf.BarConditionOkBackground
                    );
            }

            ImGui.End();
            PopWindowEditingStyle();
        }

        private void ProgressBar(float progress, int orientation, Vector2 size, Vector4 fgColor, Vector4 bgColor)
        {
            var pointTopLeft = ImGui.GetCursorScreenPos();

            var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());

            bdl.AddRectFilled(
                pointTopLeft,
                new Vector2(pointTopLeft.X + size.X, pointTopLeft.Y + size.Y),
                ImGui.GetColorU32(bgColor),
                5);

            switch (orientation)
            {
                case 0: // left to right
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
                default:
                    bdl.PushClipRect(
                        pointTopLeft,
                        new Vector2(
                            pointTopLeft.X + size.X * progress,
                            pointTopLeft.Y + size.Y
                        )
                    );
                    break;
            }

            bdl.AddRectFilled(
                pointTopLeft,
                new Vector2(pointTopLeft.X + size.X, pointTopLeft.Y + size.Y),
                ImGui.GetColorU32(fgColor),
                5);
            bdl.PopClipRect();
        }

        private void DrawSpiritbondBar()
        {
            if (!conf.BarSpiritbondEnabled) return;

            var windowFlags = PrepareWindow(position.BarSpiritbond);

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarSpiritbondSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(BarSpiritbondWindow, windowFlags))
            {
                if (spiritbond < ThresholdSpiritbondFull)
                    ProgressBar(spiritbond / 100f,
                        conf.BarSpiritbondOrientation,
                        conf.BarSpiritbondSize,
                        conf.BarSpiritbondProgressColor,
                        conf.BarSpiritbondProgressBackground
                    );
                else
                    ProgressBar(spiritbond / 100f,
                        conf.BarSpiritbondOrientation,
                        conf.BarSpiritbondSize,
                        conf.BarSpiritbondFullColor,
                        conf.BarSpiritbondFullBackground
                    );
            }

            ImGui.End();
            PopWindowEditingStyle();
        }

        private void DrawPercent(bool percentEnabled, float percent, string percentWindow, string percentWindowChild,
            Vector4 percentColor, Vector4 percentBg, Vector2 percentPosition)
        {
            DrawText(percentEnabled, $"{percent:F2}%", false, percentWindow, percentWindowChild,
                percentColor, percentBg, percentPosition);
        }

        private void DrawAlert(bool alertEnabled, string text, string alertWindow, string alertWindowChild,
            Vector4 alertColor, Vector4 alertBg, Vector2 alertPosition)
        {
            DrawText(alertEnabled, text, true, alertWindow, alertWindowChild,
                alertColor, alertBg, alertPosition);
        }

        private void DrawText(bool enabled, string text, bool isAlert, string window, string windowChild, Vector4 color,
            Vector4 bg, Vector2 position)
        {
            if (!enabled) return;

            var windowFlags = PrepareWindow(position);
            PushWindowEditingStyle(isAlert);
            if (ImGui.Begin(window, windowFlags))
            {
                var childSize = ImGui.CalcTextSize(text);
                childSize.X += ProgressLabelPadding * 2;

                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5f);
                ImGui.PushStyleColor(ImGuiCol.ChildBg, bg);

                if (ImGui.BeginChild(windowChild, childSize, false, ImGuiWindowFlags.NoInputs))
                {
                    var pos = ImGui.GetCursorPos();
                    pos.X += ProgressLabelPadding;
                    ImGui.SetCursorPos(pos);
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    ImGui.TextUnformatted(text);
                    ImGui.PopStyleColor();

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

            if (ImGui.Checkbox("Highlight##repairMe001", ref highlightCheckbox)) conf.Save();

            ImGui.SameLine();
            if (ImGui.Checkbox("Testing mode##repairMe002", ref testingMode)) conf.Save();

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Condition settings##repairMe003", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Checkbox("Bar##repairMe004", ref conf.BarConditionEnabled)) conf.Save();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(longestOrientationLabel);
                if (ImGui.Combo("Orientation##repairMe004.2", ref conf.BarConditionOrientation,
                        OrientationLabels, OrientationSize))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Percentage##repairMe005", ref conf.PercentConditionEnabled)) conf.Save();

                if (ImGui.Checkbox("Alert when Low##repairMe006", ref conf.AlertConditionLowEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Alert when Critical##repairMe007", ref conf.AlertConditionCriticalEnabled))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Position");
                if (ImGui.DragFloat2("Alert##repairMe039", ref position.AlertCondition, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                if (ImGui.DragFloat2("Percentage##repairMe040", ref position.PercentCondition, 1f, 1,
                        float.MaxValue,
                        "%.0f"))
                    conf.Save();

                if (ImGui.DragFloat2("Bar##repairMe041", ref position.BarCondition, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Size");
                if (ImGui.DragFloat2("Bar##repairMe008", ref conf.BarConditionSize, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Threshold order is OK > Low > Critical. This affects the condition bar and alerts");
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
                if (ImGui.InputTextWithHint("Alert Low Message##repairMe011", "", ref conf.AlertConditionLowText,
                        AlertMessageMaximumLength)) conf.Save();
                if (ImGui.InputTextWithHint("Alert Critical Message##repairMe012", "",
                        ref conf.AlertConditionCriticalText,
                        AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();
                ImGui.Text("Colors support transparency, including becoming fully transparent");
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
                    ColorPicker("Alert Low Color##repairMe022", ref conf.AlertConditionLowColor);
                    ColorPicker("Alert Low Background##repairMe023", ref conf.AlertConditionLowBg);
                    ColorPicker("Alert Critical Color##repairMe024", ref conf.AlertConditionCriticalColor);
                    ColorPicker("Alert Critical Background##repairMe025", ref conf.AlertConditionCriticalBg);
                    ImGui.EndTable();
                }
            }

            if (ImGui.CollapsingHeader("Spiritbond settings"))
            {
                if (ImGui.Checkbox("Bar##repairMe026", ref conf.BarSpiritbondEnabled)) conf.Save();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(longestOrientationLabel);
                if (ImGui.Combo("Orientation##repairMe026.2", ref conf.BarSpiritbondOrientation,
                        OrientationLabels, OrientationSize))
                    conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Percentage##repairMe027", ref conf.PercentSpiritbondEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Alert when Full##repairMe028", ref conf.AlertSpiritbondFullEnabled)) conf.Save();


                ImGui.Spacing();
                ImGui.Text("Position");
                if (ImGui.DragFloat2("Alert##repairMe042", ref position.AlertSpiritbond, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                if (ImGui.DragFloat2("Percentage##repairMe043", ref position.PercentSpiritbond, 1f, 1,
                        float.MaxValue,
                        "%.0f"))
                    conf.Save();

                if (ImGui.DragFloat2("Bar##repairMe044", ref position.BarSpiritbond, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Size");
                if (ImGui.DragFloat2("Bar##repairMe029", ref conf.BarSpiritbondSize, 1f, 1, float.MaxValue,
                        "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                if (ImGui.InputTextWithHint("Alert Full Message##repairMe030", "", ref conf.AlertSpiritbondFullText,
                        AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();
                ImGui.Text("Colors support transparency, including becoming fully transparent");
                if (ImGui.BeginTable("spiritbond-colors", 2))
                {
                    ColorPicker("Percentage Color##repairMe031", ref conf.PercentSpiritbondColor);
                    ColorPicker("Percentage Background##repairMe032", ref conf.PercentSpiritbondBg);
                    ColorPicker("Bar Color##repairMe033", ref conf.BarSpiritbondProgressColor);
                    ColorPicker("Bar Background##repairMe034", ref conf.BarSpiritbondProgressBackground);
                    ColorPicker("Bar Full Color##repairMe035", ref conf.BarSpiritbondFullColor);
                    ColorPicker("Bar Full Background##repairMe036", ref conf.BarSpiritbondFullBackground);
                    ColorPicker("Alert Full Color##repairMe037", ref conf.AlertSpiritbondFullColor);
                    ColorPicker("Alert Full Background##repairMe038", ref conf.AlertSpiritbondFullBg);
                    ImGui.TableNextColumn();
                    ImGui.EndTable();
                }
            }

            if (ImGui.CollapsingHeader("Resolution/positioning settings##repairMe045", ImGuiTreeNodeFlags.DefaultOpen))
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
                if(ImGui.Combo("##repairMe046", ref current, resolutions, resolutions.Length))
                {
                    selectedProfileCombo = current;
                }
                ImGui.SameLine();
                if (ImGui.Button("Copy"))
                {
                    positionUndo = new PositionProfile();
                    positionUndo.CopyFrom(position);

                    PositionProfile? source = conf.PositionProfiles.GetValueOrDefault(resolutions[selectedProfileCombo]);
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

            }

            ImGui.End();
        }

#if DEBUG
        private void DrawDebugWindow()
        {
            var e = eventHandler.EquipmentScannerLastEquipmentData;
            if (e == null) return;

            if (ImGui.Begin("RepairMe Debug",
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (ImGui.BeginTable("detail", 4, ImGuiTableFlags.BordersInnerH))
                {
                    for (int i = 0; i < EquipmentScanner.EquipmentContainerSize; i++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(i.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(e?.Id[i].ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(e?.Condition[i] / 300f):F2}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(e?.Spiritbond[i] / 100f):F2}");
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }
#endif
    }
}