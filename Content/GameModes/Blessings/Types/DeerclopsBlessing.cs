using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>独眼巨鹿·凛冬之步：冰面如履平地，移速提升，寒冷不侵</summary>
    internal sealed class DeerclopsBlessing : Blessing
    {
        public override int ProgressOrder => 70;

        public override int[] AnchorNPCTypes => [NPCID.Deerclops];

        public override string SigilPath =>
            "M50,82 L50,46 M50,62 L34,46 M50,62 L66,46 M34,46 L26,30 M66,46 L74,30 M34,46 L38,34 M66,46 L62,34";

        public override void UpdateEquips(BlessingPlayer bp) {
            Player player = bp.Player;
            player.iceSkate = true;
            player.moveSpeed += BlessingTuning.DeerclopsMoveSpeed;
            player.buffImmune[BuffID.Chilled] = true;
        }
    }
}
