using System;
using System.Numerics;
using Dalamud.Configuration;
using static RepairMe.Dalamud;

namespace RepairMe
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        // thresholds stuff

        public int ThresholdConditionLow = 50;
        public int ThresholdConditionCritical = 30;
        [NonSerialized] public readonly int ThresholdSpiritbondFull = 100;

        // bar - condition

        [NonSerialized] public readonly string BarConditionWindow = "repairme-bar-condition";
        public bool BarConditionEnabled = true;
        public Vector2 BarConditionSize = new(470, 2);

        public Vector4 BarConditionOkColor = new(0.5f, 0.9f, 0, 1);
        public Vector4 BarConditionOkBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarConditionLowColor = new(0.9f, 0.7f, 0, 1);
        public Vector4 BarConditionLowBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarConditionCriticalColor = new(0.9f, 0.2f, 0, 1);
        public Vector4 BarConditionCriticalBackground = new(0.29f, 0.29f, 0.29f, 0.54f);

        // bar - spiritbond

        [NonSerialized] public readonly string BarSpiritbondWindow = "repairme-bar-spiritbond";
        public bool BarSpiritbondEnabled = false;
        public Vector2 BarSpiritbondSize = new(470, 2);

        public Vector4 BarSpiritbondProgressColor = new(0.12f, 0.81f, 0.88f, 0.4f);
        public Vector4 BarSpiritbondProgressBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarSpiritbondFullColor = new(1, 1, 1, 1f);
        public Vector4 BarSpiritbondFullBackground = new(0.29f, 0.29f, 0.29f, 0.54f);

        // percent labels - condition

        [NonSerialized] public readonly string PercentConditionWindow = "repairme-percent-condition";
        [NonSerialized] public readonly string PercentConditionWindowChild = "repairme-percent-condition-child";
        public bool PercentConditionEnabled = true;
        public Vector4 PercentConditionColor = new(1, 1, 1, 1);
        public Vector4 PercentConditionBg = new(1, 1, 1, 0.2f);

        // percent labels - spiritbond

        [NonSerialized] public readonly string PercentSpiritbondWindow = "repairme-percent-spiritbond";
        [NonSerialized] public readonly string PercentSpiritbondWindowChild = "repairme-percent-spiritbond-child";
        public bool PercentSpiritbondEnabled = false;
        public Vector4 PercentSpiritbondColor = new(1, 1, 1, 1);
        public Vector4 PercentSpiritbondBg = new(1, 1, 1, 0.2f);

        // alerts (common)
        
        [NonSerialized] public readonly uint AlertMessageMaximumLength = 1000;

        // alerts - condition

        [NonSerialized] public readonly string AlertConditionLowWindow = "repairme-alert-condition-low";
        [NonSerialized] public readonly string AlertConditionLowWindowChild = "repairme-alert-condition-low-child";
        public bool AlertConditionLowEnabled = true;
        public string AlertConditionLowText = "Condition is low";
        public Vector4 AlertConditionLowColor = new(0.95f, 0.8f, 0.25f, 1);
        public Vector4 AlertConditionLowBg = new(1, 1, 1, 0.2f);

        [NonSerialized] public readonly string AlertConditionCriticalWindow = "repairme-alert-condition-critical";

        [NonSerialized]
        public readonly string AlertConditionCriticalWindowChild = "repairme-alert-condition-critical-child";

        public bool AlertConditionCriticalEnabled = true;
        public string AlertConditionCriticalText = "Condition is critical";
        public Vector4 AlertConditionCriticalColor = new(1f, 1f, 1f, 1);
        public Vector4 AlertConditionCriticalBg = new(0.85f, 0.05f, 0.05f, 0.25f);

        // alerts - spiritbond

        [NonSerialized] public readonly string AlertSpiritbondFullWindow = "repairme-alert-spiritbond-full";
        [NonSerialized] public readonly string AlertSpiritbondFullWindowChild = "repairme-alert-spiritbond-full-child";
        public bool AlertSpiritbondFullEnabled = false;
        public string AlertSpiritbondFullText = "Spiritbond complete";
        public Vector4 AlertSpiritbondFullColor = new(0.25f, 0.9f, 0.95f, 1);
        public Vector4 AlertSpiritbondFullBg = new(1, 1, 1, 0.2f);
        
        public int Version { get; set; } = 0;

        // the below exist just to make saving/loading less cumbersome
        [NonSerialized]
        private static Configuration? _cachedConfig;

        public void Save()
        {
            PluginInterface.SavePluginConfig(this);
        }

        public static Configuration GetOrLoad()
        {
            if (_cachedConfig != null)
                return _cachedConfig;
            
            if (PluginInterface.GetPluginConfig() is not Configuration config)
            {
                config = new Configuration();
                config.Save();
            }
            
            _cachedConfig = config;
            return _cachedConfig;
        }
    }
}