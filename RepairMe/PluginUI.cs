using System;
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

        // constants
        private const int ProgressLabelPadding = 5;
        private const int TestingModeCycleDurationInt = 15;
        private const float TestingModeCycleDurationFloat = TestingModeCycleDurationInt;

        private readonly Vector4 initialBorderColor;

        // reference fields
        private Configuration conf => Configuration.GetOrLoad();
        private readonly EventHandler eventHandler;

        // non-config ui fields
        private bool movableUiCheckbox = false;
        private bool movableUi => movableUiCheckbox && SettingsVisible;
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
                DrawPercent(conf.PercentConditionEnabled, condition, conf.PercentConditionWindow,
                    conf.PercentConditionWindowChild, conf.PercentConditionColor, conf.PercentConditionBg);

                // percent spiritbond
                DrawPercent(conf.PercentSpiritbondEnabled, spiritbond, conf.PercentSpiritbondWindow,
                    conf.PercentSpiritbondWindowChild, conf.PercentSpiritbondColor, conf.PercentSpiritbondBg);

                // alert condition critical
                if (movableUi || condition <= conf.ThresholdConditionCritical)
                    DrawAlert(conf.AlertConditionCriticalEnabled, conf.AlertConditionCriticalText,
                        conf.AlertConditionCriticalWindow, conf.AlertConditionCriticalWindowChild,
                        conf.AlertConditionCriticalColor, conf.AlertConditionCriticalBg);

                // alert condition low
                if (movableUi || condition <= conf.ThresholdConditionLow &&
                    condition > conf.ThresholdConditionCritical)
                    DrawAlert(conf.AlertConditionLowEnabled, conf.AlertConditionLowText, conf.AlertConditionLowWindow,
                        conf.AlertConditionLowWindowChild, conf.AlertConditionLowColor, conf.AlertConditionLowBg);

                // alert spiritbond full
                if (movableUi || conf.ThresholdSpiritbondFull <= spiritbond)
                    DrawAlert(conf.AlertSpiritbondFullEnabled, conf.AlertSpiritbondFullText,
                        conf.AlertSpiritbondFullWindow, conf.AlertSpiritbondFullWindowChild,
                        conf.AlertSpiritbondFullColor, conf.AlertSpiritbondFullBg);

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


        private ImGuiWindowFlags PrepareWindow()
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                         ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

            if (movableUi) return wFlags;

            wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;
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

            var windowFlags = PrepareWindow();

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarConditionSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(conf.BarConditionWindow, windowFlags))
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

            var windowFlags = PrepareWindow();

            PushWindowEditingStyle();

            ImGui.SetNextWindowSize(conf.BarSpiritbondSize + ImGui.GetStyle().WindowPadding * 2);
            if (ImGui.Begin(conf.BarSpiritbondWindow, windowFlags))
            {
                if (spiritbond < conf.ThresholdSpiritbondFull)
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
            Vector4 percentColor, Vector4 percentBg)
        {
            DrawText(percentEnabled, $"{percent:F2}%", false, percentWindow, percentWindowChild,
                percentColor, percentBg);
        }

        private void DrawAlert(bool alertEnabled, string text, string alertWindow, string alertWindowChild,
            Vector4 alertColor, Vector4 alertBg)
        {
            DrawText(alertEnabled, text, true, alertWindow, alertWindowChild,
                alertColor, alertBg);
        }

        private void DrawText(bool enabled, string text, bool isAlert, string window, string windowChild, Vector4 color,
            Vector4 bg)
        {
            if (!enabled) return;

            var windowFlags = PrepareWindow();
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

            if (ImGui.Checkbox("Move UI##repairMe001", ref movableUiCheckbox)) conf.Save();

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
                if (ImGui.DragFloat2("Bar dimensions##repairMe008", ref conf.BarConditionSize, 1f, 1, float.MaxValue,
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
                    conf.AlertMessageMaximumLength)) conf.Save();
                if (ImGui.InputTextWithHint("Alert Critical Message##repairMe012", "",
                    ref conf.AlertConditionCriticalText,
                    conf.AlertMessageMaximumLength)) conf.Save();

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
                if (ImGui.DragFloat2("Bar dimensions##repairMe029", ref conf.BarSpiritbondSize, 1f, 1, float.MaxValue,
                    "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                if (ImGui.InputTextWithHint("Alert Full Message##repairMe030", "", ref conf.AlertSpiritbondFullText,
                    conf.AlertMessageMaximumLength)) conf.Save();

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