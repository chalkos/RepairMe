using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;
using GeneralActionConstants = RepairMe.RepairMeConstants.GeneralActions;
using UiConstants = RepairMe.RepairMeConstants.Ui;

namespace RepairMe
{
    /// <summary>
    /// Holds DTR bar entry state for the plugin UI. Encapsulates entry references,
    /// last-known text/slot caches, and a <see cref="HasEntries"/> helper.
    /// </summary>
    internal sealed class DtrState
    {
        public IDtrBarEntry? ConditionEntry { get; set; }
        public IDtrBarEntry? SpiritbondEntry { get; set; }
        public string? LastConditionText { get; set; }
        public int LastConditionSlot { get; set; } = -1;
        public string? LastSpiritbondText { get; set; }
        public int LastSpiritbondSlot { get; set; } = -1;

        public (int Pct, int Slot, bool Label) ConditionKey { get; set; }
        public (int Pct, int Slot, bool Label) SpiritbondKey { get; set; }

        public bool HasEntries => ConditionEntry != null && SpiritbondEntry != null;

        public void RemoveEntries()
        {
            ConditionEntry?.Remove();
            ConditionEntry = null;
            SpiritbondEntry?.Remove();
            SpiritbondEntry = null;
            LastConditionText = null;
            LastConditionSlot = -1;
            LastSpiritbondText = null;
            LastSpiritbondSlot = -1;
        }
    }

    /// <summary>
    /// Holds alert font cache state. Encapsulates the scale-to-font dictionary,
    /// lock object, and disposal logic.
    /// </summary>
    internal sealed class AlertFontState
    {
        // Each scale builds its own font atlas, so snap to the slider step and cap the cache (LRU).
        private const int MaxHandles = 12;
        private readonly Dictionary<float, (IFontHandle Handle, long LastUsed)> _handles = new();
        private readonly object _lock = new();
        private long _tick;

        public IFontHandle? GetOrAdd(float scale, Func<float, IFontHandle> factory)
        {
            lock (_lock)
            {
                var roundedScale = MathF.Round(scale * 20f) / 20f;
                _tick++;
                if (_handles.TryGetValue(roundedScale, out var existing))
                {
                    _handles[roundedScale] = (existing.Handle, _tick);
                    return existing.Handle;
                }

                if (_handles.Count >= MaxHandles)
                {
                    var oldest = default(KeyValuePair<float, (IFontHandle Handle, long LastUsed)>);
                    var found = false;
                    foreach (var entry in _handles)
                    {
                        if (found && entry.Value.LastUsed >= oldest.Value.LastUsed) continue;
                        oldest = entry;
                        found = true;
                    }

                    oldest.Value.Handle.Dispose();
                    _handles.Remove(oldest.Key);
                }

                var result = factory(roundedScale);
                _handles[roundedScale] = (result, _tick);
                return result;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var entry in _handles.Values)
                    entry.Handle.Dispose();
                _handles.Clear();
            }
        }
    }
    internal partial class PluginUi : IOverlayHost
    {
        private bool disposed;

        bool IOverlayHost.UnlockedUiMode => _const.isUiUnlocked;
        ImGuiWindowFlags IOverlayHost.PrepareWindow(Vector2 position) => PrepareWindow(position);
        void IOverlayHost.PushWindowEditingStyle(bool minSize) => PushWindowEditingStyle(minSize);
        void IOverlayHost.PopWindowEditingStyle(bool minSize) => PopWindowEditingStyle(minSize);
        void IOverlayHost.MigratePositions(ref Vector2 position) => MigratePositions(ref position);
        void IOverlayHost.CheckDrag(ref Vector2 position) => CheckDrag(ref position);
        void IOverlayHost.DrawBar(BarState state, int orientation, Vector2 size, float rounding, float borderSize, Vector4 borderColor)
            => DrawBar(state.Progress, orientation, size, state.FgColor, state.BgColor, state.ShowPoints, state.Points, state.PointsColor, rounding, borderSize, borderColor);
        void IOverlayHost.DrawTextOverlay(string text, Vector4 color, Vector4 background, ref Vector2 position, float? scale, Action? clickAction)
            => DrawTextOverlay(text, color, background, ref position, scale, clickAction);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            var windows = new List<WindowBuilder>();
            foreach (var window in _const.windowSystem.Windows)
            {
                if (window is WindowBuilder builder)
                    windows.Add(builder);
            }

            foreach (var window in windows)
                TryDisposeUiResource(window.Dispose, "dispose window");

            TryDisposeUiResource(_const.windowSystem.RemoveAllWindows, "remove windows");

            _const.settingsWindow = null;
            _const.debugWindow = null;
            _const.overlayBuilder = null;

            TryDisposeUiResource(_const._dtr.RemoveEntries, "remove DTR entries");
            TryDisposeUiResource(_const._alertFonts.Dispose, "dispose alert fonts");
        }

        private static void TryDisposeUiResource(Action cleanup, string operation)
        {
            try
            {
                cleanup();
            }
            catch (Exception ex)
            {
                RepairMe.Log?.Warning(ex, $"[RepairMe] Failed to {operation} during UI disposal");
            }
        }

        internal void OnCommand(string command, string args)
        {
            var parts = args.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                ToggleSettingsVisibility();
                return;
            }

            var metric = parts[0].ToLowerInvariant();
            var sub = parts[1..];

            switch (metric)
            {
                case "condition":
                    HandleConditionCommand(sub, Conf);
                    break;

                case "spiritbond":
                    HandleSpiritbondCommand(sub, Conf);
                    break;

                case "missing":
                    HandleMissingGearCommand(sub, Conf);
                    break;

                default:
                    RepairMe.Log.Warning($"[RepairMe] Unknown metric: {metric}");
                    RepairMe.Chat.PrintError($"Unknown repairme metric: {metric}. Use: condition, spiritbond, missing");
                    break;
            }
        }

        internal void ToggleSettingsVisibility()
        {
            switch (eventHandler)
            {
                case { IsOccupied: true }:
                    RepairMe.Chat.PrintError("RepairMe is hidden while occupied");
                    break;
                case { IsInPvPArea: true }:
                    RepairMe.Chat.PrintError("RepairMe is disabled while in a PvP area");
                    break;
                default:
                    _const.settingsVisible = !_const.settingsVisible;
                    break;
            }
        }

        private void ToggleDebugVisibility()
        {
            var visible = !(_const.debugWindow?.IsOpen ?? _const.debugVisible);
            _const.debugVisible = visible;

            if (_const.debugWindow != null)
                _const.debugWindow.IsOpen = visible;
        }

        private void HandleConditionCommand(string[] sub, Configuration c)
        {
            if (sub.Length == 0)
            {
                // No subcommand: toggle all condition elements
                ToggleAllConditionElements(c);
                return;
            }

            var element = sub[0].ToLowerInvariant();

            switch (element)
            {
                case "bar":
                    c.BarConditionEnabled = !c.BarConditionEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Condition bar: {(c.BarConditionEnabled ? "enabled" : "disabled")}");
                    break;

                case "percent":
                    c.PercentConditionEnabled = !c.PercentConditionEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Condition percent: {(c.PercentConditionEnabled ? "enabled" : "disabled")}");
                    break;

                case "alert":
                    if (sub.Length >= 2)
                    {
                        var level = sub[1].ToLowerInvariant();
                        switch (level)
                        {
                            case "low":
                                c.AlertConditionLowEnabled = !c.AlertConditionLowEnabled;
                                c.Save();
                                RepairMe.Chat.Print($"Condition alert low: {(c.AlertConditionLowEnabled ? "enabled" : "disabled")}");
                                break;
                            case "critical":
                                c.AlertConditionCriticalEnabled = !c.AlertConditionCriticalEnabled;
                                c.Save();
                                RepairMe.Chat.Print($"Condition alert critical: {(c.AlertConditionCriticalEnabled ? "enabled" : "disabled")}");
                                break;
                            case "repairable":
                                c.AlertConditionRepairableEnabled = !c.AlertConditionRepairableEnabled;
                                c.Save();
                                RepairMe.Chat.Print($"Condition alert repairable: {(c.AlertConditionRepairableEnabled ? "enabled" : "disabled")}");
                                break;
                            default:
                                RepairMe.Chat.PrintError($"Unknown condition alert level: {level}. Use: low, critical, repairable");
                                break;
                        }
                    }
                    else
                    {
                        ToggleAllConditionAlerts(c);
                    }
                    break;

                default:
                    RepairMe.Chat.PrintError($"Unknown condition element: {element}. Use: bar, percent, alert");
                    break;
            }
        }

        private void HandleSpiritbondCommand(string[] sub, Configuration c)
        {
            if (sub.Length == 0)
            {
                // No subcommand: toggle all spiritbond elements
                ToggleAllSpiritbondElements(c);
                return;
            }

            var element = sub[0].ToLowerInvariant();

            switch (element)
            {
                case "bar":
                    c.BarSpiritbondEnabled = !c.BarSpiritbondEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Spiritbond bar: {(c.BarSpiritbondEnabled ? "enabled" : "disabled")}");
                    break;

                case "percent":
                    c.PercentSpiritbondEnabled = !c.PercentSpiritbondEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Spiritbond percent: {(c.PercentSpiritbondEnabled ? "enabled" : "disabled")}");
                    break;

                case "alert":
                    c.AlertSpiritbondFullEnabled = !c.AlertSpiritbondFullEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Spiritbond alert: {(c.AlertSpiritbondFullEnabled ? "enabled" : "disabled")}");
                    break;

                default:
                    RepairMe.Chat.PrintError($"Unknown spiritbond element: {element}. Use: bar, percent, alert");
                    break;
            }
        }

        private void HandleMissingGearCommand(string[] sub, Configuration c)
        {
            if (sub.Length == 0)
            {
                ToggleAllMissingGearElements(c);
                return;
            }

            var element = sub[0].ToLowerInvariant();

            switch (element)
            {
                case "alert":
                    c.AlertMissingGearEnabled = !c.AlertMissingGearEnabled;
                    c.Save();
                    RepairMe.Chat.Print($"Missing gear alert: {(c.AlertMissingGearEnabled ? "enabled" : "disabled")}");
                    break;

                default:
                    RepairMe.Chat.PrintError($"Unknown missing element: {element}. Use: alert");
                    break;
            }
        }

        private void ToggleAllMissingGearElements(Configuration c)
        {
            bool newState = !c.AlertMissingGearEnabled;
            c.AlertMissingGearEnabled = newState;
            c.Save();
            RepairMe.Chat.Print($"Missing gear overlays: {(newState ? "enabled" : "disabled")}");
        }

        private void ToggleAllConditionElements(Configuration c)
        {
            bool anyEnabled = c.BarConditionEnabled || c.PercentConditionEnabled ||
                              c.AlertConditionLowEnabled || c.AlertConditionCriticalEnabled ||
                              c.AlertConditionRepairableEnabled;
            bool newState = !anyEnabled;
            c.BarConditionEnabled = newState;
            c.PercentConditionEnabled = newState;
            c.AlertConditionLowEnabled = newState;
            c.AlertConditionCriticalEnabled = newState;
            c.AlertConditionRepairableEnabled = newState;
            c.Save();
            RepairMe.Chat.Print($"Condition overlays: {(newState ? "enabled" : "disabled")}");
        }

        private void ToggleAllConditionAlerts(Configuration c)
        {
            bool anyEnabled = c.AlertConditionLowEnabled || c.AlertConditionCriticalEnabled || c.AlertConditionRepairableEnabled;
            bool newState = !anyEnabled;
            c.AlertConditionLowEnabled = newState;
            c.AlertConditionCriticalEnabled = newState;
            c.AlertConditionRepairableEnabled = newState;
            c.Save();
            RepairMe.Chat.Print($"Condition alerts: {(newState ? "enabled" : "disabled")}");
        }

        private void ToggleAllSpiritbondElements(Configuration c)
        {
            bool anyEnabled = c.BarSpiritbondEnabled || c.PercentSpiritbondEnabled || c.AlertSpiritbondFullEnabled;
            bool newState = !anyEnabled;
            c.BarSpiritbondEnabled = newState;
            c.PercentSpiritbondEnabled = newState;
            c.AlertSpiritbondFullEnabled = newState;
            c.Save();
            RepairMe.Chat.Print($"Spiritbond overlays: {(newState ? "enabled" : "disabled")}");
        }

        private unsafe void OpenRepairs()
        {
            try
            {
                ActionManager.Instance()->UseAction(
                    FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, GeneralActionConstants.Repair);
            }
            catch (Exception e)
            {
                RepairMe.Log?.Error(e, "Could not use general action repair");
            }
        }

        private unsafe void OpenMateriaExtraction()
        {
            try
            {
                ActionManager.Instance()->UseAction(
                    FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, GeneralActionConstants.MateriaExtraction);
            }
            catch (Exception e)
            {
                RepairMe.Log?.Error(e, "Could not use general action materia extraction");
            }
        }

        internal void UpdateDtrBar(float lowestConditionPct, int conditionSlot, float highestSpiritbondPct, int spiritbondSlot, bool showConditionLabel, bool showSpiritbondLabel)
        {
            EnsureDtrEntries();
            if (!_const._dtr.HasEntries) return;

            var dtr = _const._dtr;
            if (conditionSlot < 0 || spiritbondSlot < 0)
            {
                dtr.LastConditionText = null;
                dtr.LastConditionSlot = -1;
                dtr.LastSpiritbondText = null;
                dtr.LastSpiritbondSlot = -1;
                return;
            }

            // Floor, like the overlays: 99.6% must not read as 100%.
            var condPct = (int)MathF.Floor(Math.Max(0f, lowestConditionPct));
            var sbPct = (int)MathF.Floor(Math.Clamp(highestSpiritbondPct, 0f, 100f));

            // Cheap early-out so this can be called every frame without formatting strings.
            if (dtr.LastConditionText != null && dtr.LastSpiritbondText != null
                && dtr.ConditionKey == (condPct, conditionSlot, showConditionLabel)
                && dtr.SpiritbondKey == (sbPct, spiritbondSlot, showSpiritbondLabel))
                return;

            var condPctStr = $"{condPct}%";
            var sbPctStr = $"{sbPct}%";
            var condText = showConditionLabel ? $"Condition: {condPctStr}" : condPctStr;
            var sbText = showSpiritbondLabel ? $"Spiritbond: {sbPctStr}" : sbPctStr;

            if (dtr.LastConditionText != condText || dtr.LastConditionSlot != conditionSlot)
            {
                dtr.ConditionEntry!.Text = condText;
                dtr.ConditionEntry!.Tooltip = new SeStringBuilder()
                    .AddText($"{GetEquipmentSlotName(conditionSlot)}: {condPctStr}")
                    .Build();
                dtr.LastConditionText = condText;
                dtr.LastConditionSlot = conditionSlot;
            }

            if (dtr.LastSpiritbondText != sbText || dtr.LastSpiritbondSlot != spiritbondSlot)
            {
                dtr.SpiritbondEntry!.Text = sbText;
                dtr.SpiritbondEntry!.Tooltip = new SeStringBuilder()
                    .AddText($"{GetEquipmentSlotName(spiritbondSlot)}: {sbPctStr}")
                    .Build();
                dtr.LastSpiritbondText = sbText;
                dtr.LastSpiritbondSlot = spiritbondSlot;
            }

            dtr.ConditionKey = (condPct, conditionSlot, showConditionLabel);
            dtr.SpiritbondKey = (sbPct, spiritbondSlot, showSpiritbondLabel);
        }

        private static string GetEquipmentSlotName(int slot)
            => slot >= 0 && slot < EquipmentConstants.SlotNames.Length
                ? EquipmentConstants.SlotNames[slot]
                : $"slot{slot}";

        private void EnsureDtrEntries()
        {
            if (_const._dtr.HasEntries) return;
            _const._dtr.ConditionEntry = RepairMe.DtrBar.Get("RepairMe Condition");
            _const._dtr.ConditionEntry.Text = "Condition: --%";
            _const._dtr.ConditionEntry.Tooltip = new SeStringBuilder()
                .AddText("Lowest equipped gear condition: --%")
                .Build();
            _const._dtr.ConditionEntry.Shown = true;

            _const._dtr.SpiritbondEntry = RepairMe.DtrBar.Get("RepairMe Spiritbond");
            _const._dtr.SpiritbondEntry.Text = "Spiritbond: --%";
            _const._dtr.SpiritbondEntry.Tooltip = new SeStringBuilder()
                .AddText("Highest equipped gear spiritbond: --%")
                .Build();
            _const._dtr.SpiritbondEntry.Shown = true;
        }

        private void CheckDrag(ref Vector2 pos)
        {
            // https://github.com/UnknownX7/QoLBar/blob/f73515daf812058d0af58cdbd0942249474e9cf8/UI/BarUI.cs
            if (_const.isDrawingFirstFrame) return;

            var dragging = !_const.isDragging
                ? ImGui.IsWindowFocused() && ImGui.IsWindowHovered() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                  ImGui.IsMouseDragging(ImGuiMouseButton.Left, 5)
                : _const.isDragging && !ImGui.IsMouseReleased(ImGuiMouseButton.Left);

            if (dragging && !_const.isDragging)
                _const.isDragging = true;

            if (_const.isDragging && ImGui.IsWindowFocused())
            {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0);
                ImGui.ResetMouseDragDelta();
                pos.X += delta.X;
                pos.Y += delta.Y;
            }

            if (!dragging && _const.isDragging)
            {
                _const.isDragging = false;
                Conf.Save();
            }
        }

        private ImGuiWindowFlags PrepareWindow(Vector2 pos)
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                         ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

            ImGuiHelpers.ForceNextWindowMainViewport();

            if (Conf.PositionsMigrated && !_const.isDragging)
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + pos);

            if (_const.isUiUnlocked) return wFlags;

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

        private BarState SelectConditionBarState(float conditionValue)
        {
            bool alt = Conf.AltCharacters.ContainsKey(PlayerState.ContentId);
            if (alt && conditionValue <= Conf.ThresholdConditionCriticalAlt
                || !alt && conditionValue <= Conf.ThresholdConditionCritical)
                return new BarState(conditionValue / 100f,
                    Conf.BarConditionCriticalColor, Conf.BarConditionCriticalBackground,
                    false, UiConstants.EmptyPoints, Vector4.Zero);
            if (alt && conditionValue <= Conf.ThresholdConditionLowAlt
                || !alt && conditionValue <= Conf.ThresholdConditionLow)
                return new BarState(conditionValue / 100f,
                    Conf.BarConditionLowColor, Conf.BarConditionLowBackground,
                    false, UiConstants.EmptyPoints, Vector4.Zero);
            return new BarState(conditionValue / 100f,
                Conf.BarConditionOkColor, Conf.BarConditionOkBackground,
                false, UiConstants.EmptyPoints, Vector4.Zero);
        }

        private BarState SelectSpiritbondBarState(float spiritbondValue)
        {
            if (spiritbondValue < UiConstants.SpiritbondFullThreshold)
                return new BarState(spiritbondValue / 100f,
                    Conf.BarSpiritbondProgressColor, Conf.BarSpiritbondProgressBackground,
                    Conf.BarSpiritbondShowAllItems, _const.spiritbondPoints, Conf.BarSpiritbondPointsColor);
            return new BarState(spiritbondValue / 100f,
                Conf.BarSpiritbondFullColor, Conf.BarSpiritbondFullBackground,
                Conf.BarSpiritbondShowAllItems, _const.spiritbondPoints, Conf.BarSpiritbondPointsColor);
        }

        private readonly record struct ConditionAlertState(
            bool IsActive,
            string Text,
            Vector4 Color,
            Vector4 Background,
            Action? ClickAction)
        {
            public static ConditionAlertState Inactive => new(false, string.Empty, Vector4.Zero, Vector4.Zero, null);
        }

        private bool IsAlt()
        {
            if (PlayerState == null) return false;
            return Conf.AltCharacters.ContainsKey(PlayerState.ContentId);
        }

        private void DrawAltCharacterList()
        {
            if (Conf.AltCharacters.Count == 0)
                return;

            ImGui.Spacing();
            ImGui.Text("Characters using the alternative thresholds");
            if (!ImGui.BeginTable("##repairMeAltChars", 2))
                return;

            ImGui.TableSetupColumn("##repairMeAltChars-c1", ImGuiTableColumnFlags.WidthFixed, 30 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Character##repairMeAltChars-c2");

            ulong removal = 0;
            foreach (var (id, name) in Conf.AltCharacters)
            {
                ImGui.TableNextColumn();
                ImGui.PushFont(UiBuilder.IconFont);
                var clicked = ImGui.Button($"{FontAwesomeIcon.Times.ToIconString()}##repairMeAltRemove-{id}");
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Shift+Click to remove from list");
                if (clicked && RepairMe.Keys[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT])
                    removal = id;

                ImGui.TableNextColumn();
                ImGui.Text(name);
            }

            ImGui.EndTable();

            if (removal != 0 && Conf.AltCharacters.Remove(removal))
                Conf.Save();
        }

        private bool AnyConditionAlertEnabled()
            => Conf.AlertConditionLowEnabled || Conf.AlertConditionCriticalEnabled || Conf.AlertConditionRepairableEnabled;

        private ConditionAlertState ComputeConditionAlertState()
        {
            var alert = _const._alertStateEvaluator.EvaluateCondition(_const.condition, IsAlt(), Conf);
            if (alert.Changed && alert.IsActive)
                CreateTransientWindow(Conf.ConditionAlertSfx);

            return _conditionAlertCache = new ConditionAlertState(
                alert.IsActive,
                alert.Text,
                alert.Color,
                alert.Background,
                GetConditionAlertClickAction(alert.Kind));
        }

        private Action? GetConditionAlertClickAction(AlertKind kind) => kind switch
        {
            AlertKind.Critical => Conf.AlertConditionCriticalShortcut ? OpenRepairs : null,
            AlertKind.Alert => Conf.AlertConditionLowShortcut ? OpenRepairs : null,
            AlertKind.Repairable => Conf.AlertConditionRepairableShortcut ? OpenRepairs : null,
            _ => null,
        };

        private bool HasActiveConditionAlert()
        {
            return _conditionAlertCache.IsActive;
        }

        private ConditionAlertState _conditionAlertCache = ConditionAlertState.Inactive;

        private readonly record struct MissingGearAlertState(
            bool IsActive,
            string Text,
            Vector4 Color,
            Vector4 Background)
        {
            public static MissingGearAlertState Inactive => new(false, string.Empty, Vector4.Zero, Vector4.Zero);
        }

        private MissingGearAlertState _missingGearAlertCache = MissingGearAlertState.Inactive;

        private MissingGearAlertState ComputeMissingGearAlertState()
        {
            if (!Conf.AlertMissingGearEnabled || !PlayerState.IsLoaded)
                return ApplyMissingGearAlert(_const._alertStateEvaluator.EvaluateMissingGear(new MissingGearResult(false, string.Empty), Conf));

            ushort classJobId = (ushort)PlayerState.ClassJob.RowId;
            byte level = (byte)PlayerState.Level;
            var key = (eventHandler.EquipmentVersion, classJobId, level, _const.Items != null);
            if (key != _missingGearKey)
            {
                _missingGearKey = key;
                _missingGearResult = EquipmentAnalysis.GetMissingGear(eventHandler.GetLastEquipmentData(), classJobId, level, _const.Items);
            }

            return ApplyMissingGearAlert(_const._alertStateEvaluator.EvaluateMissingGear(_missingGearResult, Conf));
        }

        private MissingGearAlertState ApplyMissingGearAlert(AlertEvaluationResult alert)
        {
            if (Conf.AlertMissingGearEnabled && alert.Changed && alert.IsActive)
                CreateTransientWindow(Conf.MissingAlertSfx);

            return _missingGearAlertCache = new MissingGearAlertState(
                alert.IsActive,
                alert.Text,
                alert.Color,
                alert.Background);
        }

        private bool HasMissingGearAlert()
        {
            return _missingGearAlertCache.IsActive;
        }

        private bool _spiritbondAlertActive;

        private void ComputeSpiritbondAlertState()
        {
            var active = Conf.AlertSpiritbondFullEnabled && _const.spiritbond >= UiConstants.SpiritbondFullThreshold;
            if (active && !_spiritbondAlertActive)
                CreateTransientWindow(Conf.SpiritbondAlertSfx);

            _spiritbondAlertActive = active;
        }

        private void DrawBar(float progress, int orientation, Vector2 size, Vector4 fgColor,
            Vector4 bgColor, bool showPoints, float[] points, Vector4 pointsColor,
            float rounding, float borderSize, Vector4 borderColor)
        {
            float innerOffset = borderSize;
            float outerOffset = MathF.Ceiling(borderSize / 2f) - 0.5f;
            var screenPos = ImGui.GetCursorScreenPos();
            var pointTopLeftOuter = screenPos + Vector2.One * outerOffset;
            var sizeOuter = size - Vector2.One * outerOffset * 2;
            var pointTopLeft = screenPos + Vector2.One * innerOffset;
            var drawSize = size - Vector2.One * innerOffset * 2;

            var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());

            if (borderSize > 0)
                bdl.AddRect(pointTopLeftOuter,
                    new Vector2(pointTopLeftOuter.X + sizeOuter.X, pointTopLeftOuter.Y + sizeOuter.Y),
                    ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.Closed, borderSize);

            bdl.AddRectFilled(pointTopLeft,
                new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                ImGui.GetColorU32(bgColor), rounding);

            // Inline ComputeClipRect
            (Vector2 clipMin, Vector2 clipMax) = orientation switch
            {
                0 => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X * progress, pointTopLeft.Y + drawSize.Y)),
                1 => (new Vector2(pointTopLeft.X + drawSize.X * (1 - progress), pointTopLeft.Y),
                       new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y)),
                2 => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * progress)),
                3 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * (1 - progress)),
                       new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y)),
                _ => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y))
            };
            bdl.PushClipRect(clipMin, clipMax);
            bdl.AddRectFilled(pointTopLeft,
                new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                ImGui.GetColorU32(fgColor), rounding);
            bdl.PopClipRect();

            if (!showPoints) return;

            for (int i = 0; i < points.Length; i++)
            {
                if (progress < points[i] || points[i] < 0) continue;

                // Inline ComputePointClipRect
                (Vector2 pointClipMin, Vector2 pointClipMax) = orientation switch
                {
                    0 => (new Vector2(pointTopLeft.X + drawSize.X * points[i], pointTopLeft.Y),
                           new Vector2(pointTopLeft.X + drawSize.X * points[i] + UiConstants.BarPointSize, pointTopLeft.Y + drawSize.Y)),
                    1 => (new Vector2(pointTopLeft.X + drawSize.X * (1 - points[i]), pointTopLeft.Y),
                           new Vector2(pointTopLeft.X + drawSize.X * (1 - points[i]) + UiConstants.BarPointSize, pointTopLeft.Y + drawSize.Y)),
                    2 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * points[i]),
                           new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * points[i] + UiConstants.BarPointSize)),
                    3 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * (1 - points[i])),
                           new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * (1 - points[i]) + UiConstants.BarPointSize)),
                    _ => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y))
                };
                bdl.PushClipRect(pointClipMin, pointClipMax);
                bdl.AddRectFilled(pointTopLeft,
                    new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                    ImGui.GetColorU32(pointsColor), rounding);
                bdl.PopClipRect();
            }
        }

        private void DrawTextOverlay(string text, Vector4 color, Vector4 background,
            ref Vector2 position, float? alertScale = null, Action? clickAction = null)
        {
            var fontHandle = alertScale.HasValue ? GetOrCreateAlertFont(alertScale.Value) : null;

            var baseTextSize = ImGui.CalcTextSize(text);
            var scaledTextSize = baseTextSize * (alertScale ?? 1.0f);
            var padding = Vector2.One * 2;
            padding.X += UiConstants.ProgressLabelPadding * 2;
            var childSize = scaledTextSize + padding;

            var childFlags = (_const.isUiUnlocked || clickAction != null) ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, background);

            try
            {
                if (ImGui.BeginChild("##child", childSize, false, childFlags))
                {
                    if (_const.isUiUnlocked)
                        CheckDrag(ref position);

                    var textPos = new Vector2(
                        (childSize.X - scaledTextSize.X) * 0.5f,
                        (childSize.Y - scaledTextSize.Y) * 0.5f);

                    using (fontHandle?.Push())
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.SetCursorPos(textPos);
                        ImGui.TextUnformatted(text);
                        ImGui.PopStyleColor();
                    }

                    if (!_const.isUiUnlocked && clickAction != null && ImGui.IsWindowHovered() &&
                        ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                        clickAction.Invoke();
                }
            }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
            }
        }

        private static string FormatPercentText(float value, float? extraValue, bool showSign, bool showDecimals)
        {
            string Format(float v) => showSign && showDecimals ? $"{v:F2}%"
                : showSign ? $"{Math.Floor(v):F0}%"
                : showDecimals ? $"{v:F2}"
                : $"{Math.Floor(v):F0}";

            string text = Format(value);

            if (extraValue != null)
                text = Format(extraValue.Value) + " / " + text;

            return text;
        }

        private IFontHandle? GetOrCreateAlertFont(float scale)
        {
            return _const._alertFonts.GetOrAdd(scale, roundedScale =>
            {
                var targetFontSize = UiBuilder.DefaultFontSizePx * roundedScale;
                return _pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
                    e.OnPreBuild(tk =>
                        tk.AddDalamudDefaultFont(targetFontSize)));
            });
        }

        private void MigratePositions(ref Vector2 position)
        {
            if (Conf.PositionsMigrated || !_const.isDrawingFirstFrame) return;

            var pos = ImGui.GetWindowPos() - ImGui.GetMainViewport().Pos;
            position.X = pos.X;
            position.Y = pos.Y;
        }

        private void DrawSettingsWindow()
        {
            _const.settingsWindow ??= CreateSettingsWindow();
            EnsureWindowRegistered(_const.settingsWindow);

            _const.settingsWindow.Draw(ref _const.settingsVisible);
        }

        private void DrawDebugWindow()
        {
            _const.debugWindow ??= CreateDebugWindow();
            EnsureWindowRegistered(_const.debugWindow);

            _const.debugWindow.Draw(ref _const.debugVisible);
        }

        private void SyncWindowVisibility()
        {
            if (_const.settingsWindow != null)
                _const.settingsVisible = _const.settingsWindow.IsOpen;

            if (_const.debugWindow != null)
                _const.debugVisible = _const.debugWindow.IsOpen;
        }

        private WindowItem CreateAlertSfxRow(string label, Func<uint> getValue, Action<uint> setValue, Action save, string playButtonId)
        {
            return UiItem.SameLine(
                UiItem.Width(80, UiItem.TextInput(label,
                    "",
                    () => getValue().ToString(),
                    v =>
                    {
                        if (!uint.TryParse(v, out var parsed))
                            return;

                        setValue(parsed);
                    },
                    save,
                    maxLength: 10,
                    flags: ImGuiInputTextFlags.CharsDecimal)),
                UiItem.Button("play" + playButtonId, () => CreateTransientWindow(getValue())));
        }

        private void CreateTransientWindow(uint? onOpenSfxId = null, uint? onCloseSfxId = null)
        {
            var openSfxId = onOpenSfxId ?? 0u;
            var closeSfxId = onCloseSfxId ?? 0u;
            var soundEnabled = openSfxId > 0 || closeSfxId > 0;
            var transientWindow = new WindowBuilder
            {
                Title = $"RepairMe SFX###repairme-transient-sfx-{Guid.NewGuid():N}",
                DrawOnce = true,
                IsOpen = true,
                DisableWindowSounds = !soundEnabled,
                DisableFadeInFadeOut = true,
                OnOpenSfxId = openSfxId,
                OnCloseSfxId = closeSfxId,
                Flags = ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoBackground
                    | ImGuiWindowFlags.NoInputs
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.AlwaysAutoResize,
                HeaderItems = Array.Empty<WindowItem>(),
                Tabs = Array.Empty<SettingsTab>(),
            };

            transientWindow.RemoveAction = RemoveTransientWindow;
            _const.windowSystem.AddWindow(transientWindow);
        }

        private void RemoveTransientWindow(WindowBuilder window)
        {
            window.RemoveAction = null;
            _const.windowSystem.RemoveWindow(window);
            window.MarkDisposed();
        }

        private void EnsureWindowRegistered(WindowBuilder window)
        {
            foreach (var registeredWindow in _const.windowSystem.Windows)
            {
                if (ReferenceEquals(registeredWindow, window))
                    return;
            }

            _const.windowSystem.AddWindow(window);
        }

        // Cache for Lumina lookups: item ID -> (itemLevel, itemName).
        // Item data is static per ID, so entries never need invalidating.
        private readonly Dictionary<uint, (string ItemLevel, string ItemName)> _luminaCache = new();

        private TableRootItem.TableRow[] BuildEquipmentDebugRows(EquipmentData equipmentData,
            ExcelSheet<Lumina.Excel.Sheets.Item>? itemSheet)
        {
            var rows = new List<TableRootItem.TableRow>(EquipmentConstants.ContainerSize);
            for (int slotIndex = 0; slotIndex < EquipmentConstants.ContainerSize; slotIndex++)
            {
                var rawItemId = equipmentData.Id[slotIndex];
                string itemLevel;
                string itemName;

                if (rawItemId == 0 || !_luminaCache.TryGetValue(rawItemId, out var cached))
                {
                    (itemLevel, itemName) = GetEquipmentDebugItemInfo(rawItemId, itemSheet);
                    if (rawItemId != 0)
                    {
                        _luminaCache[rawItemId] = (itemLevel, itemName);
                    }
                }
                else
                {
                    itemLevel = cached.ItemLevel;
                    itemName = cached.ItemName;
                }

                rows.Add(UiItem.TableRow(new[]
                {
                    slotIndex.ToString(),
                    EquipmentConstants.SlotNames[slotIndex],
                    rawItemId.ToString(),
                    itemLevel,
                    $"{(equipmentData.Condition[slotIndex] / EquipmentConstants.MaxConditionRawValue):F2}",
                    $"{(equipmentData.Spiritbond[slotIndex] / 100f):F2}",
                    itemName,
                }));
            }

            return rows.ToArray();
        }

        private TableRootItem.TableRow[] BuildPositionProfileDebugRows(IEnumerable<PositionProfile> profiles,
            Vector2 conditionBarSize, Vector2 spiritbondBarSize)
        {
            var rows = new List<TableRootItem.TableRow>();
            foreach (var profile in profiles)
            {
                rows.Add(UiItem.TableRow(new[]
                {
                    profile.Id,
                    FormatDebugPosition(profile.AlertCondition),
                    FormatDebugPosition(profile.PercentCondition),
                    FormatDebugPosition(profile.BarCondition),
                    FormatDebugPosition(conditionBarSize),
                    FormatDebugPosition(profile.AlertSpiritbond),
                    FormatDebugPosition(profile.PercentSpiritbond),
                    FormatDebugPosition(profile.BarSpiritbond),
                    FormatDebugPosition(spiritbondBarSize),
                }));
            }

            return rows.ToArray();
        }

        private static (string ItemLevel, string ItemName) GetEquipmentDebugItemInfo(uint rawItemId,
            ExcelSheet<Lumina.Excel.Sheets.Item>? itemSheet)
        {
            if (rawItemId == 0)
                return ("0", "Unknown");

            var baseItemId = EquipmentAnalysis.NormalizeItemId(rawItemId);
            var itemRow = baseItemId == 0 ? null : itemSheet?.GetRow(baseItemId);
            if (itemRow is not { RowId: > 0 } validItem)
                return ("N/A", "Unknown");

            return ($"{(int)validItem.LevelItem.RowId}", $"{validItem.Name}");
        }

        private static string FormatDebugPosition(Vector2 value)
            => $"{value.X}x{value.Y}";

        private readonly record struct DebugEquipmentSummary(
            int EquippedSlotCount,
            EquipmentSummary Summary,
            string LowestCondition,
            string HighestCondition,
            string LowestSpiritbond,
            string HighestSpiritbond);

        private int _debugSummaryFrame = -1;
        private DebugEquipmentSummary? _debugSummary;

        // The debug window shows many lines from one summary; build it once per frame.
        private string DebugLine(Func<DebugEquipmentSummary, string> format)
        {
            var frame = ImGui.GetFrameCount();
            if (frame != _debugSummaryFrame)
            {
                _debugSummaryFrame = frame;
                var e = eventHandler.GetLastEquipmentData();
                _debugSummary = e.HasValue ? GetDebugEquipmentSummary(e.Value) : null;
            }

            return _debugSummary is { } summary ? format(summary) : "N/A";
        }

        private DebugEquipmentSummary GetDebugEquipmentSummary(EquipmentData equipmentData)
        {
            ushort classJobId = (ushort)PlayerState.ClassJob.RowId;
            byte level = (byte)PlayerState.Level;
            float lowThresholdPercent = Conf.ThresholdConditionLow;

            int equippedSlotCount = EquipmentAnalysis.GetExpectedEquippedSlotCount(equipmentData, classJobId, level, _const.Items);
            EquipmentSummary summary = EquipmentAnalysis.GetEquipmentSummary(equipmentData, classJobId, level, lowThresholdPercent, _const.Items);
            string lowestCondition = $"{equipmentData.LowestConditionPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.LowestConditionSlot]})";
            string highestCondition = $"{equipmentData.HighestConditionPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.HighestConditionSlot]})";
            string lowestSpiritbond = $"{equipmentData.LowestSpiritbondPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.LowestSpiritbondSlot]})";
            string highestSpiritbond = $"{equipmentData.HighestSpiritbondPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.HighestSpiritbondSlot]})";

            return new DebugEquipmentSummary(equippedSlotCount, summary, lowestCondition, highestCondition, lowestSpiritbond, highestSpiritbond);
        }
    }
}
