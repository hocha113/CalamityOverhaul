using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal sealed class LegendTrialDefinition
    {
        public string Key { get; }
        public LocalizedText Title { get; }
        public LocalizedText Summary { get; }
        public ILegendTrialTarget Target { get; }

        public bool IsAvailable => Target?.IsAvailable == true;
        public bool IsCompleted => Target?.IsCompleted == true;

        public LegendTrialDefinition(string key, ILegendTrialTarget target, LocalizedText title = null, LocalizedText summary = null) {
            Key = key;
            Target = target;
            Title = title;
            Summary = summary;
        }
    }
}
