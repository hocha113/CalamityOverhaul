using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>蜂后·蜂巢再生：治疗物品的恢复量提高</summary>
    internal sealed class QueenBeeBlessing : Blessing
    {
        public override int ProgressOrder => 50;

        public override int[] AnchorNPCTypes => [NPCID.QueenBee];

        public override string SigilPath =>
            "M50,22 L74,36 L74,64 L50,78 L26,64 L26,36 Z M50,42 Q60,52 50,64 Q40,52 50,42 Z";

        public override void GetHealLife(BlessingPlayer bp, Item item, bool quickHeal, ref int healValue) {
            if (healValue > 0) {
                healValue = (int)(healValue * BlessingTuning.QueenBeeHealMult);
            }
        }
    }
}
