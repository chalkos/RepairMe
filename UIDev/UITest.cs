using System;
using System.Numerics;
using ImGuiNET;
using ImGuiScene;

namespace UIDev
{
    internal class UITest : IPluginUIMock
    {
        private SimpleImGuiScene scene;

        public void Initialize(SimpleImGuiScene scene)
        {
            scene.OnBuildUI += Draw;

            Visible = true;
            SettingsVisible = true;
            AlertsVisible = true;


            BarOkBgColor = BarLowBgColor = BarCriticalBgColor = ImGui.GetStyle().Colors[(int) ImGuiCol.FrameBg];
            BarOkColor = BarLowColor = BarCriticalColor = ImGui.GetStyle().Colors[(int) ImGuiCol.PlotHistogram];
            progressLabelContainerBgColor = new Vector4(1f, 1f, 1f, 0.2f);


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
            condition = (15 - DateTime.Now.Second % 15) / 15f;
            DrawMainWindow();
            DrawSettingsWindow();
            DrawAlertsWindow();

            if (!Visible) scene.ShouldQuit = true;
        }

        #region Nearly a copy/paste of PluginUI

        private bool visible;

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        public bool AlertsVisible { get; set; }

        private bool settingsVisible;

        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        // this is where you'd have to start mocking objects if you really want to match
        // but for simple UI creation purposes, just hardcoding values works
        private float condition;

        private bool moveableUI;
        private bool enableBar = true;
        private bool enableLabel = true;
        private bool enableAlerts = true;

        private Vector2 barSize = new(200, 10);
        private const int ProgressLabelPadding = 5;
        private int lowCondition = 60;
        private int criticalCondition = 30;
        private Vector4 BarOkBgColor;
        private Vector4 BarOkColor;
        private Vector4 BarLowColor;
        private Vector4 BarLowBgColor;
        private Vector4 BarCriticalColor;
        private Vector4 BarCriticalBgColor;
        private Vector4 progressLabelContainerBgColor;

        private const int textFieldMaxLength = 512;
        private string alertLow = "Condition is low";
        private string alertCritical = "Condition is critical";
        private Vector4 alertLowColor = new(1, 1, 1, 1);
        private Vector4 alertLowBgColor = new(1, 1, 1, 1);
        private Vector4 alertCriticalColor = new(1, 1, 1, 1);
        private Vector4 alertCriticalBgColor = new(1, 1, 1, 1);


        public void DrawMainWindow()
        {
            if (!Visible || !enableLabel && !enableBar) return;

            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            if (!moveableUI)
                wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
            if (ImGui.Begin("RepairMe", ref visible, wFlags))
            {
                if (enableLabel)
                {
                    var txt = $"{condition * 100:F2}% - Head";
                    var childSize = ImGui.CalcTextSize(txt);
                    childSize.X += ProgressLabelPadding * 2;

                    ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5f);
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, progressLabelContainerBgColor);
                    ImGui.BeginChild("progressLabelContainer", childSize);

                    var pos = ImGui.GetCursorPos();
                    pos.X += ProgressLabelPadding;
                    ImGui.SetCursorPos(pos);
                    ImGui.TextUnformatted(txt);

                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                    ImGui.PopStyleVar();
                }

                if (enableBar)
                {
                    if (condition * 100 <= criticalCondition)
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, BarCriticalColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, BarCriticalBgColor);
                    }
                    else if (condition * 100 <= lowCondition)
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, BarLowColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, BarLowBgColor);
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, BarOkColor);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, BarOkBgColor);
                    }

                    ImGui.ProgressBar(condition, barSize, "");
                    ImGui.PopStyleColor(2);
                }

                ImGui.End();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }

        public void DrawAlertsWindow()
        {
            if (!AlertsVisible) return;


            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            if (!moveableUI)
                wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(200, 100));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
            if (ImGui.Begin("RepairMeAlert", ref visible, wFlags))
            {
                if (condition * 100 <= criticalCondition)
                {
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, alertCriticalColor);
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, alertCriticalBgColor);
                    ImGui.Text(alertCritical);
                    ImGui.PopStyleColor(2);
                }
                else if (condition * 100 <= lowCondition)
                {
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, alertLowColor);
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, alertLowBgColor);
                    ImGui.Text(alertLow);
                    ImGui.PopStyleColor(2);
                }

                ImGui.End();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        public void DrawSettingsWindow()
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
                        ImGui.Checkbox("Move UI", ref moveableUI);
                        ImGui.SameLine();
                        ImGui.Checkbox("Testing mode", ref moveableUI); // use time based condition
                        ImGui.Spacing();
                        ImGui.Checkbox("Show bar", ref enableBar);
                        ImGui.SameLine();
                        ImGui.Checkbox("Show label", ref enableLabel);

                        ImGui.DragFloat2("Bar dimensions", ref barSize, 1f, 1, float.MaxValue, "%.0f");
                        ImGui.Spacing();
                        ImGui.Text("Threshold order is OK > Low > Crit. This affects the condition bar and alerts");
                        ImGui.SliderInt("Low condition threshold", ref lowCondition, 0, 100, "%d%%");
                        ImGui.SliderInt("Critical condition threshold", ref criticalCondition, 0, 100, "%d%%");
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Alerts"))
                    {
                        ImGui.Checkbox("Show alerts", ref enableAlerts);
                        ImGui.InputTextWithHint(string.Empty, alertLow, ref alertLow, textFieldMaxLength,
                            ImGuiInputTextFlags.EnterReturnsTrue);
                        ImGui.InputTextWithHint(string.Empty, alertCritical, ref alertCritical, textFieldMaxLength,
                            ImGuiInputTextFlags.EnterReturnsTrue);
                        ImGui.ColorEdit4("[Low] Alert color", ref alertLowColor);
                        ImGui.ColorEdit4("[Low] Alert background", ref alertLowBgColor);
                        ImGui.ColorEdit4("[Crit] Alert color", ref alertCriticalColor);
                        ImGui.ColorEdit4("[Crit] Alert background", ref alertCriticalBgColor);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Condition bar colors"))
                    {
                        ImGui.ColorEdit4("Text background", ref progressLabelContainerBgColor);
                        ImGui.ColorEdit4("[Ok] Bar color", ref BarOkColor);
                        ImGui.ColorEdit4("[Ok] Bar background", ref BarOkBgColor);
                        ImGui.ColorEdit4("[Low] Bar color", ref BarLowColor);
                        ImGui.ColorEdit4("[Low] Bar background", ref BarLowBgColor);
                        ImGui.ColorEdit4("[Crit] Bar color", ref BarCriticalColor);
                        ImGui.ColorEdit4("[Crit] Bar background", ref BarCriticalBgColor);
                        ImGui.EndTabItem();
                    }


                    ImGui.EndTabBar();

                    ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 40);
                    ImGui.Separator();
                    ImGui.Spacing();
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 135);

                    if (ImGui.Button("Save"))
                    {
                        // save to config
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Save & Close")) // save to config
                        SettingsVisible = false;
                }

                ImGui.End();
            }
        }

        #endregion
    }
}