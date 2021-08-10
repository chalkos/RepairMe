using System;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using ImGuiNET;

namespace RepairMe
{
    internal class PluginUi : IDisposable
    {
        // constants
        private const float TextBgCornerRounding = 5f;
        private const int ProgressLabelPadding = 5;
        private const int TestingModeCycleDurationInt = 15;
        private const float TestingModeCycleDurationFloat = TestingModeCycleDurationInt;
        private const int AlertMaxLength = 512;

        // reference fields
        private readonly Configuration conf;
        private readonly EventHandler eh;

        // non-config ui fields
        private bool movableUi;
        private float condition = 100;
        private float spiritbond = 0;
        private bool testingMode = true;

        public PluginUi(Configuration conf, EventHandler eventHandler)
        {
            this.conf = conf;
            eh = eventHandler;
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
                if (!eh.IsActive) return;

                if (settingsVisible && testingMode)
                {
                    condition = (TestingModeCycleDurationInt - DateTime.Now.Second % TestingModeCycleDurationInt) /
                        TestingModeCycleDurationFloat * 100;
                    spiritbond = (DateTime.Now.Second % TestingModeCycleDurationInt + 1) /
                        TestingModeCycleDurationFloat * 100;
                }
                else
                {
                    condition = eh.EquipmentScannerLastEquipmentData.LowestConditionPercent;
                    spiritbond = eh.EquipmentScannerLastEquipmentData.HighestSpiritbondPercent;
                }


#if DEBUG
                DrawDebugWindow();
#endif
                DrawConditionBarWindow();
                DrawAlertsWindow();
                DrawSettingsWindow();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "prevented GUI crash");
            }
        }


        private void partialDrawText(string text, Vector4 textColor, Vector4 backgroundColor, float scale = 1)
        {
            var childSize = ImGui.CalcTextSize(text);
            childSize.X += ProgressLabelPadding * 2;

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, TextBgCornerRounding);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, backgroundColor);
            ImGui.BeginChild("progressLabelContainer", childSize, false, ImGuiWindowFlags.NoInputs);

            var pos = ImGui.GetCursorPos();
            pos.X += ProgressLabelPadding;
            ImGui.SetCursorPos(pos);

            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        private void DrawConditionBarWindow()
        {
            if (!conf.EnableLabel && !conf.EnableBar) return;

            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            if (!movableUi)
                wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;


            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
            if (ImGui.Begin("RepairMe", wFlags))
            {
                if (conf.EnableLabel)
                    partialDrawText($"{condition:F2}%", ImGuiColors.White, conf.ProgressLabelContainerBgColor);

                var barPosition = ImGui.GetCursorPos();
                if (conf.EnableBar)
                {
                    if (condition <= conf.CriticalCondition)
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, conf.BarCriticalColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, conf.BarCriticalBgColor);
                    }
                    else if (condition <= conf.LowCondition)
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, conf.BarLowColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, conf.BarLowBgColor);
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, conf.BarOkColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, conf.BarOkBgColor);
                    }

                    ImGui.ProgressBar(condition / 100, conf.BarSize, "");
                    ImGui.PopStyleColor(2);
                    barPosition.Y += conf.BarSize.Y + conf.BarSpacing;
                }

                if (conf.EnableSpiritbond)
                {
                    var stylePops = 0;
                    if (spiritbond < 100f)
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, conf.SbarColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, conf.SbarBgColor);
                        stylePops = 2;
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, conf.SbarFullColor);
                        stylePops = 1;
                    }

                    ImGui.SetCursorPos(barPosition);
                    ImGui.ProgressBar(spiritbond / 100, conf.BarSize, "");
                    ImGui.PopStyleColor(stylePops);
                }

                ImGui.End();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }

        private void DrawAlertsWindow()
        {
            if (!conf.EnableAlerts) return;

            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            if (!movableUi)
                wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(200, 100));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
            if (ImGui.Begin("RepairMeAlert", wFlags))
            {
                if (condition <= conf.CriticalCondition)
                    partialDrawText(conf.AlertCritical, conf.AlertCriticalColor, conf.AlertCriticalBgColor,
                        conf.AlertScale.X);
                else if (condition <= conf.LowCondition)
                    partialDrawText(conf.AlertLow, conf.AlertLowColor, conf.AlertLowBgColor, conf.AlertScale.X);

                ImGui.End();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        private void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;

            ImGui.SetNextWindowSize(new Vector2(500, 290), ImGuiCond.Always);
            if (ImGui.Begin("RepairMe config", ref settingsVisible,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
            {
                if (ImGui.BeginTabBar("configTabs"))
                {
                    if (ImGui.BeginTabItem("Condition bar"))
                    {
                        ImGui.Checkbox("Move UI", ref movableUi);
                        ImGui.SameLine();
                        ImGui.Checkbox("Testing mode", ref testingMode); // use time based condition
                        ImGui.Spacing();

                        var enableBar = conf.EnableBar;
                        if (ImGui.Checkbox("Show bar", ref enableBar))
                            conf.EnableBar = enableBar;

                        ImGui.SameLine();

                        var enableLabel = conf.EnableLabel;
                        if (ImGui.Checkbox("Show label", ref enableLabel))
                            conf.EnableLabel = enableLabel;

                        var barSize = conf.BarSize;
                        if (ImGui.DragFloat2("Bar dimensions", ref barSize, 1f, 1, float.MaxValue, "%.0f"))
                            conf.BarSize = barSize;

                        ImGui.Spacing();
                        ImGui.Text("Threshold order is OK > Low > Crit. This affects the condition bar and alerts");

                        var lowCondition = conf.LowCondition;
                        if (ImGui.SliderInt("Low condition threshold", ref lowCondition, 0, 100, "%d%%"))
                            conf.LowCondition = lowCondition;

                        var criticalCondition = conf.CriticalCondition;
                        if (ImGui.SliderInt("Critical condition threshold", ref criticalCondition, 0, 100, "%d%%"))
                            conf.CriticalCondition = criticalCondition;

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Alerts"))
                    {
                        var enableAlerts = conf.EnableAlerts;
                        if (ImGui.Checkbox("Show alerts", ref enableAlerts))
                            conf.EnableAlerts = enableAlerts;

                        var alertLow = conf.AlertLow;

                        ImGui.PushItemWidth(160f);
                        if (ImGui.InputTextWithHint(string.Empty, "[Low] Alert text", ref alertLow, AlertMaxLength,
                            ImGuiInputTextFlags.CtrlEnterForNewLine))
                            conf.AlertLow = alertLow;

                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 3f);

                        var alertCritical = conf.AlertCritical;
                        if (ImGui.InputTextWithHint("[Low][Crit] Alert text", "[Crit] Alert text",
                            ref alertCritical,
                            AlertMaxLength,
                            ImGuiInputTextFlags.EnterReturnsTrue))
                            conf.AlertCritical = alertCritical;
                        ImGui.PopItemWidth();

                        /*
                         // TODO: add different/larger fonts for  
                        var alertScale = conf.AlertScale;
                        if (ImGui.DragFloat2("Alert text scale", ref alertScale, 0.1f, 1, 10, "%.0f"))
                            conf.AlertScale = alertScale;
                        */
                        var alertLowColor = conf.AlertLowColor;
                        if (ImGui.ColorEdit4("[Low] Alert color", ref alertLowColor))
                            conf.AlertLowColor = alertLowColor;

                        var alertLowBgColor = conf.AlertLowBgColor;
                        if (ImGui.ColorEdit4("[Low] Alert background", ref alertLowBgColor))
                            conf.AlertLowBgColor = alertLowBgColor;

                        var alertCriticalColor = conf.AlertCriticalColor;
                        if (ImGui.ColorEdit4("[Crit] Alert color", ref alertCriticalColor))
                            conf.AlertCriticalColor = alertCriticalColor;

                        var alertCriticalBgColor = conf.AlertCriticalBgColor;
                        if (ImGui.ColorEdit4("[Crit] Alert background", ref alertCriticalBgColor))
                            conf.AlertCriticalBgColor = alertCriticalBgColor;
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Condition bar colors"))
                    {
                        var progressLabelContainerBgColor = conf.ProgressLabelContainerBgColor;
                        if (ImGui.ColorEdit4("Text background", ref progressLabelContainerBgColor))
                            conf.ProgressLabelContainerBgColor = progressLabelContainerBgColor;

                        var barOkColor = conf.BarOkColor;
                        if (ImGui.ColorEdit4("[Ok] Bar color", ref barOkColor))
                            conf.BarOkColor = barOkColor;

                        var barOkBgColor = conf.BarOkBgColor;
                        if (ImGui.ColorEdit4("[Ok] Bar background", ref barOkBgColor))
                            conf.BarOkBgColor = barOkBgColor;

                        var barLowColor = conf.BarLowColor;
                        if (ImGui.ColorEdit4("[Low] Bar color", ref barLowColor))
                            conf.BarLowColor = barLowColor;

                        var barLowBgColor = conf.BarLowBgColor;
                        if (ImGui.ColorEdit4("[Low] Bar background", ref barLowBgColor))
                            conf.BarLowBgColor = barLowBgColor;

                        var barCriticalColor = conf.BarCriticalColor;
                        if (ImGui.ColorEdit4("[Crit] Bar color", ref barCriticalColor))
                            conf.BarCriticalColor = barCriticalColor;

                        var barCriticalBgColor = conf.BarCriticalBgColor;
                        if (ImGui.ColorEdit4("[Crit] Bar background", ref barCriticalBgColor))
                            conf.BarCriticalBgColor = barCriticalBgColor;
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Spiritbond"))
                    {
                        var enableSpiritbond = conf.EnableSpiritbond;
                        if (ImGui.Checkbox("Show spiritbond", ref enableSpiritbond))
                            conf.EnableSpiritbond = enableSpiritbond;

                        var barSpacing = conf.BarSpacing;
                        if (ImGui.DragInt("Bar spacing", ref barSpacing, 1, -1000, 1000, "%d"))
                            conf.BarSpacing = barSpacing;

                        var sbarColor = conf.SbarColor;
                        if (ImGui.ColorEdit4("Spiritbond color", ref sbarColor))
                            conf.SbarColor = sbarColor;

                        var sbarBgColor = conf.SbarBgColor;
                        if (ImGui.ColorEdit4("Spiritbond background", ref sbarBgColor))
                            conf.SbarBgColor = sbarBgColor;

                        var sbarFullColor = conf.SbarFullColor;
                        if (ImGui.ColorEdit4("Spiritbond full color", ref sbarFullColor))
                            conf.SbarFullColor = sbarFullColor;

                        ImGui.EndTabItem();
                    }


                    ImGui.EndTabBar();

                    ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 40);
                    ImGui.Separator();
                    ImGui.Spacing();
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 135);

                    if (ImGui.Button("Save")) conf.Save();

                    ImGui.SameLine();
                    if (ImGui.Button("Save & Close"))
                    {
                        conf.Save();
                        SettingsVisible = false;
                    }
                }

                ImGui.End();
            }
        }

#if DEBUG
        private void DrawDebugWindow()
        {
            var e = eh.EquipmentScannerLastEquipmentData;

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

                ImGui.End();
            }
        }
#endif
    }
}