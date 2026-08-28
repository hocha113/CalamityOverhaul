using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>石巨人·岩心：石壳加身，防御提升且不受击退</summary>
    internal sealed class GolemBlessing : Blessing
    {
        public override int ProgressOrder => 140;

        public override int[] AnchorNPCTypes => [NPCID.Golem];

        public override string SigilPath =>
            "M32,26 L68,26 L68,62 L58,62 L58,72 L42,72 L42,62 L32,62 Z M50,38 L50,52 M42,44 L58,44";

        public override void UpdateEquips(BlessingPlayer bp) {
            Player player = bp.Player;
            player.statDefense += BlessingTuning.GolemDefense;
            player.noKnockback = true;
        }
    }
}
