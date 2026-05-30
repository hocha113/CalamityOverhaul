namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal readonly struct LegendTrialTargetSnapshot
    {
        public readonly bool IsActive;
        public readonly float Progress;
        public readonly float DisplayRatio;
        public readonly string ActiveName;
        public readonly string StatusLine;

        public LegendTrialTargetSnapshot(bool isActive, float progress, float displayRatio, string activeName, string statusLine = "") {
            IsActive = isActive;
            Progress = MathHelper.Clamp(progress, 0f, 1f);
            DisplayRatio = MathHelper.Clamp(displayRatio, 0f, 1f);
            ActiveName = activeName ?? string.Empty;
            StatusLine = statusLine ?? string.Empty;
        }

        public static LegendTrialTargetSnapshot Inactive => new(false, 0f, 1f, string.Empty);
        public static LegendTrialTargetSnapshot Completed => new(false, 1f, 0f, string.Empty);
    }
}
