using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using static RepairMe.Dalamud;

namespace RepairMe
{

    [Serializable]
    public class PositionProfile
    {
        public int ResolutionWidth = 0;
        public int ResolutionHeight = 0;
        public string Id => ResolutionWidth + "x" + ResolutionHeight;

        public Vector2 PercentCondition = new Vector2(50, 50);
        public Vector2 AlertLowCondition = new Vector2(250, 50);
        public Vector2 AlertCriticalCondition = new Vector2(250, 75);
        public Vector2 BarCondition = new Vector2(50, 100);
        
        public Vector2 BarSpiritbond = new Vector2(50, 150);
        public Vector2 PercentSpiritbond = new Vector2(250, 150);
        public Vector2 AlertSpiritbond = new Vector2(50, 200);

        public void CopyFrom(PositionProfile other)
        {
            PercentCondition = other.PercentCondition;
            AlertLowCondition = other.AlertLowCondition;
            AlertCriticalCondition = other.AlertCriticalCondition;
            BarCondition = other.BarCondition;
            
            BarSpiritbond = other.BarSpiritbond;
            PercentSpiritbond = other.PercentSpiritbond;
            AlertSpiritbond = other.AlertSpiritbond;
        }
    }


    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool PositionsMigrated = true;

        public bool HideUiWhenOccupied = true;

        // thresholds stuff
        public int ThresholdConditionLow = 50;
        public int ThresholdConditionCritical = 30;

        // bar - condition
        public bool BarConditionEnabled = true;
        public float BarConditionRounding = 5f;
        public Vector2 BarConditionSize = new(470, 2);
        public int BarConditionOrientation = 0;
        public float BarConditionBorderSize = 0;

        public Vector4 BarConditionOkColor = new(0.5f, 0.9f, 0, 1);
        public Vector4 BarConditionOkBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarConditionLowColor = new(0.9f, 0.7f, 0, 1);
        public Vector4 BarConditionLowBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarConditionCriticalColor = new(0.9f, 0.2f, 0, 1);
        public Vector4 BarConditionCriticalBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarConditionBorderColor = new(0, 0, 0, 1);

        // bar - spiritbond
        public bool BarSpiritbondEnabled = false;
        public float BarSpiritbondRounding = 5f;
        public bool BarSpiritbondShowAllItems = false;
        public Vector2 BarSpiritbondSize = new(470, 2);
        public int BarSpiritbondOrientation = 0;
        public float BarSpiritbondBorderSize = 0;

        public Vector4 BarSpiritbondProgressColor = new(0.12f, 0.81f, 0.88f, 0.4f);
        public Vector4 BarSpiritbondPointsColor = new(0.9f, 0.7f, 0, 1);
        public Vector4 BarSpiritbondProgressBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarSpiritbondFullColor = new(1, 1, 1, 1f);
        public Vector4 BarSpiritbondFullBackground = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarSpiritbondBorderColor = new(0, 0, 0, 1);

        // percent labels - condition
        public bool PercentConditionEnabled = true;
        public bool PercentConditionShowPercent = true;
        public bool PercentConditionShowDecimals = false;
        public Vector4 PercentConditionColor = new(1, 1, 1, 1);
        public Vector4 PercentConditionBg = new(1, 1, 1, 0.2f);

        // percent labels - spiritbond
        public bool PercentSpiritbondEnabled = false;
        public bool PercentSpiritbondShowPercent = true;
        public bool PercentSpiritbondShowDecimals = false;
        public bool PercentSpiritbondShowMinMax = false;
        public Vector4 PercentSpiritbondColor = new(1, 1, 1, 1);
        public Vector4 PercentSpiritbondBg = new(1, 1, 1, 0.2f);

        // alerts - condition
        public bool AlertConditionLowEnabled = true;
        public string AlertConditionLowText = "Condition is low";
        public Vector4 AlertConditionLowColor = new(0.95f, 0.8f, 0.25f, 1);
        public Vector4 AlertConditionLowBg = new(1, 1, 1, 0.2f);
        public bool AlertConditionCriticalEnabled = true;
        public string AlertConditionCriticalText = "Condition is critical";
        public Vector4 AlertConditionCriticalColor = new(1f, 1f, 1f, 1);
        public Vector4 AlertConditionCriticalBg = new(0.85f, 0.05f, 0.05f, 0.25f);
        public bool AlertConditionLowShortcut = false;
        public bool AlertConditionCriticalShortcut = false;

        // alerts - spiritbond
        public bool AlertSpiritbondFullEnabled = false;
        public string AlertSpiritbondFullText = "Spiritbond complete";
        public Vector4 AlertSpiritbondFullColor = new(0.25f, 0.9f, 0.95f, 1);
        public Vector4 AlertSpiritbondFullBg = new(1, 1, 1, 0.2f);
        public bool AlertSpiritbondShortcut = false;
        
        // positioning
        public Dictionary<string, PositionProfile> PositionProfiles = new();

        // the below exist just to make saving/loading less cumbersome
        [NonSerialized] private static Configuration? _cachedConfig;

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
            else
            {
                // migrate versions
                if (config.Version == 0)
                {
                    config.PositionsMigrated = false;
                    config.Version = 1;
                }
            }

            _cachedConfig = config;
            return _cachedConfig;
        }
    }
}