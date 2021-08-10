using System;
using System.Numerics;
using ImGuiNET;
using ImGuiScene;

namespace UIDev
{
    internal class UITest : IPluginUIMock
    {
        private SimpleImGuiScene scene;
        private const int ProgressLabelPadding = 5;
        private const int TestingModeCycleDurationInt = 15;
        private const float TestingModeCycleDurationFloat = TestingModeCycleDurationInt;

        private float spiritbond;
        private float condition;

        private bool moveableUi = false;
        private bool testingMode = true;

        private Vector4 initialBorderColor;

        private Configuration conf;


        public void Initialize(SimpleImGuiScene scene)
        {
            conf = new Configuration();

            initialBorderColor = ImGui.GetStyle().Colors[(int) ImGuiCol.Border];

            scene.OnBuildUI += Draw;

            SettingsVisible = true;

            // saving this only so we can kill the test application by closing the window
            // (instead of just by hitting escape)
            this.scene = scene;
        }

        public void Dispose()
        {
        }

        public static void Main(string[] args)
        {
            UIBootstrap.Inititalize(new UITest());
        }

        // You COULD go all out here and make your UI generic and work on interfaces etc, and then
        // mock dependencies and conceivably use exactly the same class in this testbed and the actual plugin
        // That is, however, a bit excessive in general - it could easily be done for this sample, but I
        // don't want to imply that is easy or the best way to go usually, so it's not done here either
        private void Draw()
        {
            if (SettingsVisible && testingMode)
            {
                condition = (TestingModeCycleDurationInt - DateTime.Now.Second % TestingModeCycleDurationInt) /
                    TestingModeCycleDurationFloat * 100;
                spiritbond = (DateTime.Now.Second % TestingModeCycleDurationInt + 1) /
                    TestingModeCycleDurationFloat * 100;
            }
            else
            {
                condition = 45;
                spiritbond = 90;
            }


            // bar condition
            DrawConditionBar();

            // bar spiritbond
            DrawSpiritbondBar();

            // percent condition
            DrawPercent(conf.PercentConditionEnabled, condition, conf.PercentConditionWindow,
                conf.PercentConditionWindowChild, conf.PercentConditionColor,
                conf.PercentConditionBg);

            // percent spiritbond
            DrawPercent(conf.PercentSpiritbondEnabled, spiritbond, conf.PercentSpiritbondWindow,
                conf.PercentSpiritbondWindowChild, conf.PercentSpiritbondColor,
                conf.PercentSpiritbondBg);

            // alert condition critical
            if (moveableUi || condition <= conf.ThresholdConditionCritical)
                DrawAlert(conf.AlertConditionCriticalEnabled, conf.AlertConditionCriticalText,
                    conf.AlertConditionCriticalWindow, conf.AlertConditionCriticalWindowChild,
                    conf.AlertConditionCriticalColor,
                    conf.AlertConditionCriticalBg);

            // alert condition low
            if (moveableUi || condition <= conf.ThresholdConditionLow && condition > conf.ThresholdConditionCritical)
                DrawAlert(conf.AlertConditionLowEnabled, conf.AlertConditionLowText, conf.AlertConditionLowWindow,
                    conf.AlertConditionLowWindowChild, conf.AlertConditionLowColor,
                    conf.AlertConditionLowBg);

            // alert spiritbond full
            if (moveableUi || conf.ThresholdSpiritbondFull <= spiritbond)
                DrawAlert(conf.AlertSpiritbondFullEnabled, conf.AlertSpiritbondFullText, conf.AlertSpiritbondFullWindow,
                    conf.AlertSpiritbondFullWindowChild,
                    conf.AlertSpiritbondFullColor, conf.AlertSpiritbondFullBg);

            DrawSettingsWindow();
        }


        #region Nearly a copy/paste of PluginUI

        private bool settingsVisible;

        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        // this is where you'd have to start mocking objects if you really want to match
        // but for simple UI creation purposes, just hardcoding values works


        private ImGuiWindowFlags PrepareWindow(string windowName)
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            if (moveableUi) return wFlags;

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

            var windowFlags = PrepareWindow(conf.BarConditionWindow);

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

                ImGui.End();
            }

            PopWindowEditingStyle();
        }

        private void DrawSpiritbondBar()
        {
            if (!conf.BarSpiritbondEnabled) return;

            var windowFlags = PrepareWindow(conf.BarSpiritbondWindow);

            PushWindowEditingStyle();

            if (ImGui.Begin(conf.BarSpiritbondWindow, windowFlags))
            {
                if (spiritbond < conf.ThresholdSpiritbondFull)
                    PushBarColors(conf.BarSpiritbondProgressColor, conf.BarSpiritbondProgressBackground);
                else
                    PushBarColors(conf.BarSpiritbondFullColor, conf.BarSpiritbondFullBackground);

                ImGui.ProgressBar(spiritbond / 100f, conf.BarSpiritbondSize, "");

                PopBarColors();

                ImGui.End();
            }

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

            var windowFlags = PrepareWindow(window);
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

                    ImGui.EndChild();
                }

                ImGui.PopStyleColor();
                ImGui.PopStyleVar();

                ImGui.End();
            }

            PopWindowEditingStyle(isAlert);
        }

        private void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;

            ImGui.SetColorEditOptions(ImGuiColorEditFlags.NoInputs);
            ImGui.PushStyleColor(ImGuiCol.Border, initialBorderColor);
            ImGui.SetNextWindowSize(new Vector2(510, 525), ImGuiCond.Always);
            if (!ImGui.Begin("RepairMe config", ref settingsVisible, ImGuiWindowFlags.NoResize))
            {
                ImGui.End();
                return;
            }

            if (ImGui.Checkbox("Move UI", ref moveableUi)) conf.Save();

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
                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Percentage Color", ref conf.PercentConditionColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Percentage Background", ref conf.PercentConditionBg)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Color", ref conf.BarConditionOkColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Background", ref conf.BarConditionOkBackground)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Low Color", ref conf.BarConditionLowColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Low Background", ref conf.BarConditionLowBackground)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Critical Color", ref conf.BarConditionCriticalColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Critical Background", ref conf.BarConditionCriticalBackground))
                        conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Low Color", ref conf.AlertConditionLowColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Low Background", ref conf.AlertConditionLowBg)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Critical Color", ref conf.AlertConditionCriticalColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Critical Background", ref conf.AlertConditionCriticalBg)) conf.Save();
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
                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Percentage Color", ref conf.PercentSpiritbondColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Percentage Background", ref conf.PercentSpiritbondBg)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Color", ref conf.BarSpiritbondProgressColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Background", ref conf.BarSpiritbondProgressBackground)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Full Color", ref conf.BarSpiritbondFullColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Bar Full Background", ref conf.BarSpiritbondFullBackground)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Full Color", ref conf.AlertSpiritbondFullColor)) conf.Save();

                    ImGui.TableNextColumn();
                    if (ImGui.ColorEdit4("Alert Full Background", ref conf.AlertSpiritbondFullBg)) conf.Save();
                    ImGui.TableNextColumn();
                    ImGui.EndTable();
                }
            }

            ImGui.End();
            ImGui.PopStyleColor();
        }

        #endregion
    }
}