using CalamityMod.Items.Armor.Victide;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    // 潮胜
    internal class ModifyVictide : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<VictideHeadRanged>();
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ModContent.ItemType<VictideBreastplate>() || legs.type != ModContent.ItemType<VictideGreaves>()) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "8%";
            player.CWR().KreloadTimeIncrease -= 0.08f;
        }
    }
}
