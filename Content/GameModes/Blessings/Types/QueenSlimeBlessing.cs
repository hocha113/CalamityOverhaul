using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>史莱姆皇后·晶羽：坠落无伤，跃身更高</summary>
    internal sealed class QueenSlimeBlessing : Blessing
    {
        public override int ProgressOrder => 90;

        public override int[] AnchorNPCTypes => [NPCID.QueenSlimeBoss];

        public override string SigilPath =>
            "M50,18 L63,44 L50,82 L37,44 Z M63,44 L78,36 M37,44 L22,36";

        public override void UpdateEquips(BlessingPlayer bp) {
            Player player = bp.Player;
            player.noFallDmg = true;
            player.jumpSpeedBoost += BlessingTuning.QueenSlimeJumpBoost;
        }
    }
}
