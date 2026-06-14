using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>空降仓子世界生物群系</summary>
    internal class DropPodBiome : ModBiome
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Environment;
        public override bool IsBiomeActive(Player player) => DropPodWorld.Active;
    }
}
