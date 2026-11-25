using System.Numerics;

namespace RepairMe
{
    internal enum AlertKind
    {
        Normal,
        Critical,
        Alert,
        Repairable,
    }

    internal readonly record struct AlertEvaluationResult(
        bool IsActive,
        AlertKind Kind,
        string Text,
        Vector4 Color,
        Vector4 Background,
        bool Changed)
    {
        public static AlertEvaluationResult Inactive(bool changed = false)
            => new(false, AlertKind.Normal, string.Empty, Vector4.Zero, Vector4.Zero, changed);
    }

    internal sealed class AlertStateEvaluator
    {
        private AlertKind _lastConditionAlertKind = AlertKind.Normal;
        private AlertKind _lastMissingGearAlertKind = AlertKind.Normal;

        internal AlertEvaluationResult EvaluateCondition(float condition, bool isAlt, Configuration conf)
        {
            var newKind = GetConditionAlertKind(condition, isAlt, conf);
            var changed = newKind != _lastConditionAlertKind;

            _lastConditionAlertKind = newKind;
            return BuildConditionAlert(newKind, conf, changed);
        }

        internal AlertEvaluationResult EvaluateMissingGear(MissingGearResult missingGear, Configuration conf)
        {
            if (!missingGear.IsMissing)
            {
                var changed = _lastMissingGearAlertKind != AlertKind.Normal;
                _lastMissingGearAlertKind = AlertKind.Normal;
                return AlertEvaluationResult.Inactive(changed);
            }

            string formattedText = FormatMissingGearText(conf.AlertMissingGearText, missingGear.SlotText);
            var changedToAlert = _lastMissingGearAlertKind != AlertKind.Alert;
            _lastMissingGearAlertKind = AlertKind.Alert;
            return new AlertEvaluationResult(
                true,
                AlertKind.Alert,
                formattedText,
                conf.AlertMissingGearColor,
                conf.AlertMissingGearBg,
                changedToAlert);
        }

        private static AlertKind GetConditionAlertKind(float condition, bool isAlt, Configuration conf)
        {
            var criticalThreshold = isAlt ? conf.ThresholdConditionCriticalAlt : conf.ThresholdConditionCritical;
            var lowThreshold = isAlt ? conf.ThresholdConditionLowAlt : conf.ThresholdConditionLow;

            if (condition <= criticalThreshold)
                return conf.AlertConditionCriticalEnabled ? AlertKind.Critical : AlertKind.Normal;

            if (InRange(condition, criticalThreshold, lowThreshold))
                return conf.AlertConditionLowEnabled ? AlertKind.Alert : AlertKind.Normal;

            if (condition < 100 && condition > lowThreshold)
                return conf.AlertConditionRepairableEnabled ? AlertKind.Repairable : AlertKind.Normal;

            return AlertKind.Normal;
        }

        private static AlertEvaluationResult BuildConditionAlert(AlertKind kind, Configuration conf, bool changed)
        {
            return kind switch
            {
                AlertKind.Critical => new AlertEvaluationResult(
                    true,
                    kind,
                    conf.AlertConditionCriticalText,
                    conf.AlertConditionCriticalColor,
                    conf.AlertConditionCriticalBg,
                    changed),
                AlertKind.Alert => new AlertEvaluationResult(
                    true,
                    kind,
                    conf.AlertConditionLowText,
                    conf.AlertConditionLowColor,
                    conf.AlertConditionLowBg,
                    changed),
                AlertKind.Repairable => new AlertEvaluationResult(
                    true,
                    kind,
                    conf.AlertConditionRepairableText,
                    conf.AlertConditionRepairableColor,
                    conf.AlertConditionRepairableBg,
                    changed),
                _ => AlertEvaluationResult.Inactive(changed),
            };
        }

        private static bool InRange(float value, float belowExclusive, float atOrBelowInclusive)
            => value <= atOrBelowInclusive && value > belowExclusive;

        private static string FormatMissingGearText(string configuredText, string slotName)
        {
            var text = string.IsNullOrEmpty(configuredText) ? "Equipment missing: {slot}" : configuredText;
            return text.Replace("{slot}", slotName);
        }
    }
}
