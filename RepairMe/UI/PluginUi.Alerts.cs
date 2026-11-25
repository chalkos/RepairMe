using System;
using System.Numerics;
using UiConstants = RepairMe.RepairMeConstants.Ui;

namespace RepairMe
{
    internal partial class PluginUi
    {
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
                return ApplyMissingGearAlert(_const._alertStateEvaluator.EvaluateMissingGear(new MissingGearResult(false, string.Empty), 0, Environment.TickCount64, Conf));

            ushort classJobId = (ushort)PlayerState.ClassJob.RowId;
            byte level = (byte)PlayerState.Level;
            var key = (tracker.EquipmentVersion, classJobId, level, _const.Items != null);
            if (key != _missingGearKey)
            {
                _missingGearKey = key;
                var equipment = tracker.GetLastEquipmentData();
                var offHandExpected = EquipmentAnalysis.ResolveOffHandExpected(equipment, classJobId, level, _const.Items);
                _missingGearResult = EquipmentAnalysis.GetMissingGear(equipment, offHandExpected);
            }

            return ApplyMissingGearAlert(_const._alertStateEvaluator.EvaluateMissingGear(_missingGearResult, classJobId, Environment.TickCount64, Conf));
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
    }
}
