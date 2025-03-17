using CalamityMod.Items.Armor.GodSlayer;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyGodSlayer : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<GodSlayerHeadRanged>();
        public override int BodyID => ModContent.ItemType<GodSlayerChestplate>();
        public override int LegsID => ModContent.ItemType<GodSlayerLeggings>();
        public override float KreloadTimeIncreaseValue => 0.32f;
    }
}
