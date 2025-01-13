using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Events.TungstenRiotEvent
{
    internal class TungstenBiome : ModBiome
    {
        public override string BestiaryIcon => CWRConstant.Asset + "Events/TungstenRiotIcon";
        public override string BackgroundPath => CWRConstant.Asset + "Events/TungstenRiotBackgrounds";
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
    }
}
