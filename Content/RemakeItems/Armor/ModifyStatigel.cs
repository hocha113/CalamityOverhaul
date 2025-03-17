using CalamityMod.Items.Armor.Statigel;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyStatigel : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<StatigelHeadRanged>();
        public override int BodyID => ModContent.ItemType<StatigelArmor>();
        public override int LegsID => ModContent.ItemType<StatigelGreaves>();
        public override float KreloadTimeIncreaseValue => 0.12f;
    }
}
