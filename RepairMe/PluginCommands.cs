using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using GeneralActionConstants = RepairMe.RepairMeConstants.GeneralActions;

namespace RepairMe
{
    internal partial class PluginUi
    {
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
    }
}
