using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>猪龙鱼公爵·怒潮：残血时掀起怒潮，移速与伤害俱增</summary>
    internal sealed class DukeFishronBlessing : Blessing
    {
        public override int ProgressOrder => 150;

        public override int[] AnchorNPCTypes => [NPCID.DukeFishron];

        public override string SigilPath =>
            "M18,62 Q30,42 42,56 Q52,68 62,52 Q72,38 82,48 M52,28 L64,42 L46,42 Z";

        public override void UpdateEquips(BlessingPlayer bp) {
            Player player = bp.Player;
            if (player.statLife >= player.statLifeMax2 * BlessingTuning.FishronLifeThreshold) {
                return;
            }
            player.moveSpeed += BlessingTuning.FishronMoveSpeed;
            player.GetDamage(DamageClass.Generic) += BlessingTuning.FishronDamage;
        }
    }
}
