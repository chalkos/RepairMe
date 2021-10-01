using System;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;

namespace RepairMe
{
    internal class PluginUi : IDisposable
    {
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
                    condition = eventHandler.EquipmentScannerLastEquipmentData.LowestConditionPercent;
                    spiritbond = eventHandler.EquipmentScannerLastEquipmentData.HighestSpiritbondPercent;
                }

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
                         ImGuiWindowFlags.NoFocusOnAppearing;

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

            if (ImGui.Begin(conf.BarConditionWindow, windowFlags))
            {
                if (condition <= conf.ThresholdConditionCritical)
                    PushBarColors(conf.BarConditionCriticalColor, conf.BarConditionCriticalBackground);
                else if (condition <= conf.ThresholdConditionLow)
                    PushBarColors(conf.BarConditionLowColor, conf.BarConditionLowBackground);
                else
                    PushBarColors(conf.BarConditionOkColor, conf.BarConditionOkBackground);

                ImGui.ProgressBar(condition / 100f, conf.BarConditionSize, "");

                PopBarColors();
            }

            ImGui.End();
            PopWindowEditingStyle();
        }

        private void DrawSpiritbondBar()
        {
            if (!conf.BarSpiritbondEnabled) return;

            var windowFlags = PrepareWindow();

            PushWindowEditingStyle();

            if (ImGui.Begin(conf.BarSpiritbondWindow, windowFlags))
            {
                if (spiritbond < conf.ThresholdSpiritbondFull)
                    PushBarColors(conf.BarSpiritbondProgressColor, conf.BarSpiritbondProgressBackground);
                else
                    PushBarColors(conf.BarSpiritbondFullColor, conf.BarSpiritbondFullBackground);

                ImGui.ProgressBar(spiritbond / 100f, conf.BarSpiritbondSize, "");

                PopBarColors();
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

            ImGui.SetNextWindowSize(new Vector2(510, 525), ImGuiCond.Always);
            if (!ImGui.Begin("RepairMe config", ref settingsVisible, ImGuiWindowFlags.NoResize))
            {
                ImGui.End();
                return;
            }

            if (ImGui.Checkbox("Move UI", ref movableUiCheckbox)) conf.Save();

            ImGui.SameLine();
            if (ImGui.Checkbox("Testing mode", ref testingMode)) conf.Save();

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Condition settings", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Checkbox("Bar", ref conf.BarConditionEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Percentage", ref conf.PercentConditionEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Alert when Low", ref conf.AlertConditionLowEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Alert when Critical", ref conf.AlertConditionCriticalEnabled)) conf.Save();

                ImGui.Spacing();
                if (ImGui.DragFloat2("Bar dimensions", ref conf.BarConditionSize, 1f, 1, float.MaxValue, "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                ImGui.Text("Threshold order is OK > Low > Critical. This affects the condition bar and alerts");
                if (ImGui.SliderInt("Low threshold", ref conf.ThresholdConditionLow, 0, 100, "%d%%"))
                {
                    if (conf.ThresholdConditionLow <= conf.ThresholdConditionCritical)
                        conf.ThresholdConditionLow = conf.ThresholdConditionCritical + 1;
                    conf.Save();
                }

                if (ImGui.SliderInt("Critical threshold", ref conf.ThresholdConditionCritical, 0, 100, "%d%%"))
                {
                    if (conf.ThresholdConditionCritical >= conf.ThresholdConditionLow)
                        conf.ThresholdConditionCritical = conf.ThresholdConditionLow - 1;
                    conf.Save();
                }

                ImGui.Spacing();
                if (ImGui.InputTextWithHint("Alert Low Message", "", ref conf.AlertConditionLowText,
                    conf.AlertMessageMaximumLength)) conf.Save();
                if (ImGui.InputTextWithHint("Alert Critical Message", "", ref conf.AlertConditionCriticalText,
                    conf.AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();
                ImGui.Text("Colors support transparency, including becoming fully transparent");
                if (ImGui.BeginTable("condition-colors", 2))
                {
                    ColorPicker("Percentage Color", ref conf.PercentConditionColor);
                    ColorPicker("Percentage Background", ref conf.PercentConditionBg);
                    ColorPicker("Bar Color", ref conf.BarConditionOkColor);
                    ColorPicker("Bar Background", ref conf.BarConditionOkBackground);
                    ColorPicker("Bar Low Color", ref conf.BarConditionLowColor);
                    ColorPicker("Bar Low Background", ref conf.BarConditionLowBackground);
                    ColorPicker("Bar Critical Color", ref conf.BarConditionCriticalColor);
                    ColorPicker("Bar Critical Background", ref conf.BarConditionCriticalBackground);
                    ColorPicker("Alert Low Color", ref conf.AlertConditionLowColor);
                    ColorPicker("Alert Low Background", ref conf.AlertConditionLowBg);
                    ColorPicker("Alert Critical Color", ref conf.AlertConditionCriticalColor);
                    ColorPicker("Alert Critical Background", ref conf.AlertConditionCriticalBg);
                    ImGui.EndTable();
                }
            }

            if (ImGui.CollapsingHeader("Spiritbond settings"))
            {
                if (ImGui.Checkbox("Bar", ref conf.BarSpiritbondEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Percentage", ref conf.PercentSpiritbondEnabled)) conf.Save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Alert when Full", ref conf.AlertSpiritbondFullEnabled)) conf.Save();

                ImGui.Spacing();
                if (ImGui.DragFloat2("Bar dimensions", ref conf.BarSpiritbondSize, 1f, 1, float.MaxValue, "%.0f"))
                    conf.Save();

                ImGui.Spacing();
                if (ImGui.InputTextWithHint("Alert Full Message", "", ref conf.AlertSpiritbondFullText,
                    conf.AlertMessageMaximumLength)) conf.Save();

                ImGui.Spacing();
                ImGui.Text("Colors support transparency, including becoming fully transparent");
                if (ImGui.BeginTable("spiritbond-colors", 2))
                {
                    ColorPicker("Percentage Color", ref conf.PercentSpiritbondColor);
                    ColorPicker("Percentage Background", ref conf.PercentSpiritbondBg);
                    ColorPicker("Bar Color", ref conf.BarSpiritbondProgressColor);
                    ColorPicker("Bar Background", ref conf.BarSpiritbondProgressBackground);
                    ColorPicker("Bar Full Color", ref conf.BarSpiritbondFullColor);
                    ColorPicker("Bar Full Background", ref conf.BarSpiritbondFullBackground);
                    ColorPicker("Alert Full Color", ref conf.AlertSpiritbondFullColor);
                    ColorPicker("Alert Full Background", ref conf.AlertSpiritbondFullBg);
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
                        ImGui.Text(e.Id[i].ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text((e.Condition[i] / 300f).ToString("F2"));
                        ImGui.TableNextColumn();
                        ImGui.Text((e.Spiritbond[i] / 100f).ToString("F2"));
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }
#endif
    }
}