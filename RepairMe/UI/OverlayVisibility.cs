namespace RepairMe
{
    internal static class OverlayVisibility
    {
        /// <summary>
        /// An alert overlay is shown when it is enabled and either triggered or the UI is unlocked,
        /// so it can still be dragged into place while nothing is wrong.
        /// </summary>
        internal static bool ShouldShowAlert(bool enabled, bool triggered, bool unlocked)
            => enabled && (triggered || unlocked);
    }
}
