using System;
using System.Numerics;
using Newtonsoft.Json;

namespace RepairMe
{
    [Serializable]
    public class PositionProfile
    {
        public int ResolutionWidth = 0;
        public int ResolutionHeight = 0;
        public string Id => $"{ResolutionWidth}x{ResolutionHeight}";

        public Vector2 PercentCondition = new(50, 50);
        public Vector2 AlertCondition = new(250, 50);
        public Vector2 AlertMissingGear = new(250, 200);
        public Vector2 BarCondition = new(50, 100);
        public Vector2 ConditionLabel = new(50, 75);

        public Vector2 BarSpiritbond = new(50, 150);
        public Vector2 PercentSpiritbond = new(250, 150);
        public Vector2 AlertSpiritbond = new(50, 200);
        public Vector2 SpiritbondLabel = new(250, 100);

        public void CopyFrom(PositionProfile other)
        {
            PercentCondition = other.PercentCondition;
            AlertCondition = other.AlertCondition;
            AlertMissingGear = other.AlertMissingGear;
            BarCondition = other.BarCondition;
            ConditionLabel = other.ConditionLabel;
            BarSpiritbond = other.BarSpiritbond;
            PercentSpiritbond = other.PercentSpiritbond;
            AlertSpiritbond = other.AlertSpiritbond;
            SpiritbondLabel = other.SpiritbondLabel;
        }

        // Pre-1.1 positions: read from old configs only (never written back), then folded into AlertCondition.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Vector2? AlertLowCondition;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Vector2? AlertCriticalCondition;

        internal void MigrateLegacyAlertPositions()
        {
            if (AlertLowCondition.HasValue || AlertCriticalCondition.HasValue)
                AlertCondition = AlertLowCondition ?? AlertCriticalCondition!.Value;

            AlertLowCondition = null;
            AlertCriticalCondition = null;
        }
    }
}
