namespace RaidRoulette.Runtime
{
    // The shared engine must remain loaded for task packs while standalone events are paused.
    public static class RaidSelectionPolicy
    {
        public static string? Resolve(bool enabled, string? manual, string? configured, string? campaign)
        {
            if (!enabled)
            {
                return campaign ?? "normal";
            }

            return string.IsNullOrWhiteSpace(manual) && string.IsNullOrWhiteSpace(configured) ? campaign : manual;
        }
    }
}
