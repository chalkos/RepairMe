using System;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using GameDataConstants = RepairMe.RepairMeConstants.GameData;
using PlayerConstants = RepairMe.RepairMeConstants.Player;

namespace RepairMe
{
    /// <summary>
    /// Player state helpers used by equipment checks and debug display.
    /// </summary>
    internal static class PlayerInfo
    {
        internal static string GetPlayerDisplayName(IPlayerState playerState)
        {
            if (!playerState.IsLoaded)
                return "N/A";

            return $"{playerState.CharacterName} @ {playerState.CurrentWorld.Value.Name}";
        }

        /// <summary>
        /// Returns the current class/job abbreviation from the ClassJobAbbrevs lookup.
        /// </summary>
        internal static string GetClassJobAbbreviation(ushort classJobId)
        {
            return PlayerConstants.ClassJobAbbrevs.TryGetValue(classJobId, out var abbr) ? abbr : "Unknown";
        }

        internal static string GetClassJobAbbreviation(IPlayerState playerState)
        {
            return GetClassJobAbbreviation((ushort)playerState.ClassJob.RowId);
        }

        internal static string GetClassJobFullName(IPlayerState playerState, ExcelSheet<Lumina.Excel.Sheets.ClassJob>? classJobs)
        {
            if (!playerState.IsLoaded)
                return "N/A";

            var classJobRow = classJobs?.GetRow(playerState.ClassJob.RowId);
            return classJobRow is { RowId: > 0 } validClassJob
                ? CapitalizeFirst($"{validClassJob.Name}")
                : "Unknown";
        }

        /// <summary>
        /// Returns true if the current job requires an offhand item.
        /// </summary>
        internal static bool IsOffHandExpected(ushort classJobId, byte level, uint mainHandItemId, Func<uint, uint> itemCategoryOf)
        {
            // Fisher special case: spearfishing gig requires level >= 61.
            if (classJobId == 18)
                return level >= 61;

            if (PlayerConstants.OneHandedCasterJobs.Contains(classJobId))
                return mainHandItemId != 0
                       && IsOneHandedCasterCategory(itemCategoryOf(EquipmentAnalysis.NormalizeItemId(mainHandItemId)));

            return PlayerConstants.JobsRequiringOffHand.Contains(classJobId);
        }

        internal static bool IsOneHandedCasterCategory(uint itemUiCategory)
            => itemUiCategory == GameDataConstants.ItemCategoryOneHandedThaumaturge
               || itemUiCategory == GameDataConstants.ItemCategoryOneHandedConjurer;

        /// <summary>Looks up an item's ItemUICategory row id (0 when the item is unknown).</summary>
        internal static Func<uint, uint> ItemCategoryLookup(ExcelSheet<Lumina.Excel.Sheets.Item> sheet)
            => normalizedItemId =>
            {
                var itemRow = sheet.GetRow(normalizedItemId);
                return itemRow.RowId == 0 ? 0u : itemRow.ItemUICategory.RowId;
            };

        private static string CapitalizeFirst(string value)
        {
            return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}
