using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace RepairMe
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        // the below exist just to make saving less cumbersome

        [NonSerialized] private DalamudPluginInterface? pi;

        public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;


        public bool EnableBar { get; set; } = true;
        public bool EnableSpiritbond { get; set; } = false;
        public bool EnableLabel { get; set; } = true;
        public bool EnableAlerts { get; set; } = true;


        public Vector2 BarSize { get; set; } = new(200, 10);
        public int BarSpacing { get; set; } = 3;

        public int LowCondition { get; set; } = 50;
        public int CriticalCondition { get; set; } = 30;


        public string AlertLow { get; set; } = "Condition is low";
        public string AlertCritical { get; set; } = "Condition is critical";
        public Vector2 AlertScale { get; set; } = new(2f, 3.5f);
        public Vector4 AlertLowColor { get; set; } = new(0.9f, 0.7f, 0, 1);
        public Vector4 AlertLowBgColor { get; set; } = new(1, 1, 1, 0.2f);
        public Vector4 AlertCriticalColor { get; set; } = new(0.9f, 0.2f, 0, 1);
        public Vector4 AlertCriticalBgColor { get; set; } = new(1, 1, 1, 0.2f);

        public Vector4 BarOkColor { get; set; } = new(0.5f, 0.9f, 0, 1);
        public Vector4 BarOkBgColor { get; set; } = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarLowColor { get; set; } = new(0.9f, 0.7f, 0, 1);
        public Vector4 BarLowBgColor { get; set; } = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 BarCriticalColor { get; set; } = new(0.9f, 0.2f, 0, 1);
        public Vector4 BarCriticalBgColor { get; set; } = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 ProgressLabelContainerBgColor { get; set; } = new(1, 1, 1, 0.2f);

        public Vector4 SbarColor { get; set; } = new(1, 1, 1, 0.2f);
        public Vector4 SbarBgColor { get; set; } = new(0.29f, 0.29f, 0.29f, 0.54f);
        public Vector4 SbarFullColor { get; set; } = new(1, 1, 1, 1f);
        
        public int Version { get; set; } = 0;


        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;
        }

        public void Save()
        {
            pi?.SavePluginConfig(this);
        }
    }
}