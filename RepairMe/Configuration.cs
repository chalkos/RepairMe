using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
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

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public const int CurrentVersion = 2;

        public int Version { get; set; } = CurrentVersion;
        public bool PositionsMigrated = true;
        public bool HideUiWhenOccupied = true;
        public Dictionary<ulong, string> AltCharacters = new();
        public int ThresholdConditionLow = 50;
        public int ThresholdConditionCritical = 30;
        public int ThresholdConditionLowAlt = 50;
        public int ThresholdConditionCriticalAlt = 30;

        // bar defaults
        public static readonly Vector4 DefaultBarBackground = new(0.29f, 0.29f, 0.29f, 0.54f);

        // bar - condition
        public bool BarConditionEnabled = true;
        public float BarConditionRounding = 5f;
        public Vector2 BarConditionSize = new(470, 2);
        public int BarConditionOrientation = 0;
        public float BarConditionBorderSize = 0;
        public Vector4 BarConditionOkColor = new(0.5f, 0.9f, 0, 1);
        public Vector4 BarConditionOkBackground = DefaultBarBackground;
        public Vector4 BarConditionLowColor = new(0.9f, 0.7f, 0, 1);
        public Vector4 BarConditionLowBackground = DefaultBarBackground;
        public Vector4 BarConditionCriticalColor = new(0.9f, 0.2f, 0, 1);
        public Vector4 BarConditionCriticalBackground = DefaultBarBackground;
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
        public Vector4 BarSpiritbondProgressBackground = DefaultBarBackground;
        public Vector4 BarSpiritbondFullColor = new(1, 1, 1, 1f);
        public Vector4 BarSpiritbondFullBackground = DefaultBarBackground;
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
        public bool AlertConditionRepairableEnabled = false;
        public string AlertConditionRepairableText = "Equipment needs repair";
        public Vector4 AlertConditionRepairableColor = new(1, 1, 1, 1);
        public Vector4 AlertConditionRepairableBg = new(1, 1, 1, 0.2f);
        public bool AlertConditionRepairableShortcut = false;
        public bool AlertConditionLowEnabled = true;
        public string AlertConditionLowText = "Condition is low";
        public Vector4 AlertConditionLowColor = new(0.95f, 0.8f, 0.25f, 1);
        public Vector4 AlertConditionLowBg = new(1, 1, 1, 0.2f);
        public bool AlertConditionCriticalEnabled = true;
        public string AlertConditionCriticalText = "Condition is critical";
        public Vector4 AlertConditionCriticalColor = new(1, 1, 1, 1);
        public Vector4 AlertConditionCriticalBg = new(0.85f, 0.05f, 0.05f, 0.25f);
        public bool AlertConditionLowShortcut = false;
        public bool AlertConditionCriticalShortcut = false;
        public float AlertConditionScale = 1.0f;

        // condition label overlay
        public bool ConditionLabelEnabled = false;
        public string ConditionLabelText = "Condition";
        public Vector4 ConditionLabelColor = new(1, 1, 1, 1);
        public Vector4 ConditionLabelBg = new(1, 1, 1, 0.2f);
        public float ConditionLabelScale = 1.0f;

        // spiritbond - item level filter
        public int MinSpiritbondItemLevel = 1;

        // alerts - spiritbond
        public bool AlertSpiritbondFullEnabled = false;
        public string AlertSpiritbondFullText = "Spiritbond complete";
        public Vector4 AlertSpiritbondFullColor = new(0.25f, 0.9f, 0.95f, 1);
        public Vector4 AlertSpiritbondFullBg = new(1, 1, 1, 0.2f);
        public bool AlertSpiritbondShortcut = false;
        public float AlertSpiritbondFullScale = 1.0f;

        // spiritbond label overlay
        public bool SpiritbondLabelEnabled = false;
        public string SpiritbondLabelText = "Spiritbond";
        public Vector4 SpiritbondLabelColor = new(1, 1, 1, 1);
        public Vector4 SpiritbondLabelBg = new(1, 1, 1, 0.2f);
        public float SpiritbondLabelScale = 1.0f;

        // missing gear alert
        public bool AlertMissingGearEnabled = false;
        public string AlertMissingGearText = "Equipment slot empty";
        public Vector4 AlertMissingGearColor = new(0.9f, 0.2f, 0.2f, 1);
        public Vector4 AlertMissingGearBg = new(1, 1, 1, 0.2f);
        public float AlertMissingGearScale = 1.0f;
        public bool AlertMissingGearShortcut = false;

        // alert sounds
        public uint ConditionAlertSfx = 39;
        public uint SpiritbondAlertSfx = 39;
        public uint MissingAlertSfx = 39;

        // DTR bar slot labels
        public bool ShowDtrConditionLabel = true;
        public bool ShowDtrSpiritbondLabel = true;

        // positioning
        public Dictionary<string, PositionProfile> PositionProfiles = new(4);

        /// <summary>
        /// Flag indicating the config was freshly created and needs an initial save.
        /// Set during lazy initialization, but Save() is called outside the lazy factory
        /// to avoid re-entrant deadlock when PluginInterface.SavePluginConfig triggers GetOrLoad().
        /// </summary>
        private static bool _needsInitialSave = false;

        private static readonly Lazy<Configuration> _lazyConfig = new Lazy<Configuration>(() =>
        {
            Configuration config;

            // Defer PluginInterface access: only read from disk inside the factory.
            // PluginInterface may be null during construction (before Dalamud injection),
            // so we check for null and defer the save to GetOrLoad() time.
            var pluginInterface = RepairMe.PluginInterface;
            if (pluginInterface?.GetPluginConfig() is not Configuration loadedCfg)
            {
                config = new Configuration();
                _needsInitialSave = true;
            }
            else
            {
                config = loadedCfg;
                // migrate versions
                if (config.Version == 0)
                {
                    config.PositionsMigrated = false;
                    config.Version = 1;
                }

                if (config.Version < 2)
                {
                    foreach (var profile in config.PositionProfiles.Values)
                        profile.MigrateLegacyAlertPositions();
                    config.Version = CurrentVersion;
                    _needsInitialSave = true;
                }
            }

            return config;
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        public void Save()
        {
            try
            {
                var pluginInterface = RepairMe.PluginInterface;
                if (pluginInterface != null)
                    pluginInterface.SavePluginConfig(this);
            }
            catch (Exception ex)
            {
                RepairMe.Log.Warning(ex, "[RepairMe] Failed to save plugin configuration");
            }
        }

        public static Configuration GetOrLoad()
        {
            var config = _lazyConfig.Value;

            // Perform initial save outside the lazy factory to avoid deadlock.
            // If SavePluginConfig triggers a re-entrant GetOrLoad(), the _needsInitialSave
            // flag will already be consumed, preventing infinite recursion.
            if (_needsInitialSave)
            {
                _needsInitialSave = false;
                config.Save();
            }

            return config;
        }
    }
}
