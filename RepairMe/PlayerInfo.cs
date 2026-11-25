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
        internal static bool IsOffHandExpected(ushort classJobId, byte level, uint mainHandItemId, ExcelSheet<Lumina.Excel.Sheets.Item> sheet)
        {
            // Fisher special case: spearfishing gig requires level >= 61.
            if (classJobId == 18)
                return level >= 61;

            if (PlayerConstants.OneHandedCasterJobs.Contains(classJobId))
                return IsOneHandedCasterMainHand(mainHandItemId, sheet);

            return PlayerConstants.JobsRequiringOffHand.Contains(classJobId);
        }

        private static bool IsOneHandedCasterMainHand(uint mainHandItemId, ExcelSheet<Lumina.Excel.Sheets.Item> sheet)
        {
            if (mainHandItemId == 0)
                return false;

            var itemRow = sheet.GetRow(EquipmentAnalysis.NormalizeItemId(mainHandItemId));
            if (itemRow.RowId == 0)
                return false;

            var category = itemRow.ItemUICategory.RowId;
            return category == GameDataConstants.ItemCategoryOneHandedThaumaturge
                   || category == GameDataConstants.ItemCategoryOneHandedConjurer;
        }

        private static string CapitalizeFirst(string value)
        {
            return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}
