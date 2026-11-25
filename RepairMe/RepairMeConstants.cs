using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Interface.Windowing;
using Lumina.Excel;

namespace RepairMe
{
    internal static class RepairMeConstants
    {
        internal static class Commands
        {
            internal const string PluginCommand = "/repairme";
        }

        internal static class Equipment
        {
            internal const int ContainerSize = 13;
            internal const float MaxConditionRawValue = 300f;
            internal const float MaxSpiritbondRawValue = 10000f;

            // Slot indices match the FFXIV equipped-items container order.
            // Keep these aligned with SlotNames.
            internal const int SlotMainHand = 0;
            internal const int SlotOffHand = 1;
            internal const int SlotHead = 2;
            internal const int SlotBody = 3;
            internal const int SlotHands = 4;
            internal const int SlotLegs = 6;
            internal const int SlotFeet = 7;
            internal const int SlotEar = 8;
            internal const int SlotNeck = 9;
            internal const int SlotBracer = 10;
            internal const int SlotRing1 = 11;
            internal const int SlotRing2 = 12;

            internal static readonly string[] SlotNames =
            {
                "Main Hand",
                "Off Hand",
                "Head",
                "Body",
                "Hands",
                "Belt",
                "Legs",
                "Feet",
                "Ear",
                "Neck",
                "Bracer",
                "Ring 1",
                "Ring 2"
            };

            internal static readonly int[] CoreExpectedSlots =
            {
                SlotMainHand,
                SlotHead,
                SlotBody,
                SlotHands,
                SlotLegs,
                SlotFeet,
                SlotEar,
                SlotNeck,
                SlotBracer,
                SlotRing1,
                SlotRing2,
            };
        }

        internal static class GameData
        {
            // Lumina ItemUICategory RowIds for one-handed caster weapons (Thaumaturge's / Conjurer's Arm).
            internal const uint ItemCategoryOneHandedThaumaturge = 6;
            internal const uint ItemCategoryOneHandedConjurer = 8;
        }

        internal static class GeneralActions
        {
            // These IDs may change on game patches. Update if FFXIV modifies the GeneralAction table.
            // TODO: Validate at runtime or look up from Lumina GeneralAction sheet if IDs become unreliable.
            internal const uint Repair = 6;
            internal const uint MateriaExtraction = 14;
        }

        internal static class Ui
        {
            internal static PluginUiDefaults CreatePluginUiDefaults() => new();

            internal const int AlertMessageMaximumLength = 200;
            internal const float BarPointSize = 1f;
            internal const float DefaultConditionPercent = 100f;
            internal const int NoSelectedProfile = -1;
            internal const string NoResolutionId = "";
            internal const float TestingModeCycleDuration = 15f;
            internal const int ProgressLabelPadding = 5;
            internal const int SpiritbondFullThreshold = 100;
            internal const string WindowSystemName = "RepairMe";
            internal const bool InitialDrawingFirstFrame = true;
            internal static readonly float[] EmptyPoints = Array.Empty<float>();
            internal static readonly string[] OrientationLabels =
            {
                "Left to right",
                "Right to left",
                "Top to bottom",
                "Bottom to top",
            };

            internal sealed class PluginUiDefaults
            {
                internal PositionProfile position = new();
                internal DtrState _dtr = new();
                internal AlertFontState _alertFonts = new();
                internal bool UnlockedUiModeCheckbox;
                internal bool isUiUnlocked => UnlockedUiModeCheckbox && settingsVisible;
                internal float condition = DefaultConditionPercent;
                internal float spiritbond;
                internal float leastSpiritbond;
                internal Stopwatch _testingTimer = Stopwatch.StartNew();
                internal float[] spiritbondPoints = EmptyPoints;
                internal bool _testingMode;
                internal bool _prevTestingMode;
                internal bool isDragging;
                internal bool isDrawingFirstFrame = InitialDrawingFirstFrame;
                internal string _currentResolutionId = NoResolutionId;
                internal ExcelSheet<Lumina.Excel.Sheets.Item>? _items;
                internal ExcelSheet<Lumina.Excel.Sheets.ClassJob>? _classJobs;
                internal ExcelSheet<Lumina.Excel.Sheets.Item>? Items => _items ??= RepairMe.GameData?.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                internal ExcelSheet<Lumina.Excel.Sheets.ClassJob>? ClassJobs => _classJobs ??= RepairMe.GameData?.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
                internal AlertStateEvaluator _alertStateEvaluator = new();
                internal WindowSystem windowSystem = new(WindowSystemName);
                internal OverlayBuilder? overlayBuilder;
                internal bool settingsVisible;
                internal bool debugVisible;
                internal WindowBuilder? settingsWindow;
                internal WindowBuilder? debugWindow;
                internal int selectedProfileCombo = NoSelectedProfile;
                internal PositionProfile? positionUndo;
            }
        }

        internal static class Scanner
        {
            // Minimum interval between equipment scans. 500ms balances responsiveness vs CPU usage.
            internal const int ThrottleIntervalMs = 500;
        }

        internal static class Player
        {
            // Maps ClassJob RowId to its short abbreviation.
            internal static readonly Dictionary<ushort, string> ClassJobAbbrevs = new()
            {
                { 0, "Adventurer" },
                { 1, "GLA" }, { 2, "PGL" }, { 3, "MRD" }, { 4, "LNC" }, { 5, "ARC" }, { 6, "CNJ" }, { 7, "THM" },
                { 8, "CRP" }, { 9, "BSM" }, { 10, "ARM" }, { 11, "GSM" }, { 12, "LTW" }, { 13, "WVR" },
                { 14, "ALC" }, { 15, "CUL" }, { 16, "MIN" }, { 17, "BTN" }, { 18, "FSH" },
                { 19, "PLD" }, { 20, "MNK" }, { 21, "WAR" }, { 22, "DRG" }, { 23, "BRD" }, { 24, "WHM" }, { 25, "BLM" },
                { 26, "ACN" }, { 27, "SMN" }, { 28, "SCH" }, { 29, "ROG" },
                { 30, "NIN" }, { 31, "MCH" }, { 32, "DRK" }, { 33, "AST" }, { 34, "SAM" },
                { 35, "RDM" }, { 36, "BLU" }, { 37, "GNB" }, { 38, "DNC" }, { 39, "RPR" },
                { 40, "SGE" }, { 41, "VPR" }, { 42, "PCT" },
            };

            // Jobs that always use a shield/offhand in addition to a main hand weapon.
            // Fisher is excluded; it requires OffHand only at level >= 61.
            internal static readonly HashSet<ushort> JobsRequiringOffHand = new()
            {
                // Tank / melee with shield
                1,  // Gladiator
                19, // Paladin

                // Crafters use tools in offhand
                8,  // Carpenter
                9,  // Blacksmith
                10, // Armorer
                11, // Goldsmith
                12, // Leatherworker
                13, // Weaver
                14, // Alchemist
                15, // Culinarian

                // Gatherers use tools in offhand
                16, // Miner
                17  // Botanist
            };

            // Legacy one-handed caster jobs (CNJ/WHM, THM/BLM).
            internal static readonly HashSet<ushort> OneHandedCasterJobs = new()
            {
                6,  // Conjurer
                7,  // Thaumaturge
                24, // White Mage
                25  // Black Mage
            };
        }
    }
}
