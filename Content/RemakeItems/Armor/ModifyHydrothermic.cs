using CalamityMod.Items.Armor.Hydrothermic;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyHydrothermic : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<HydrothermicHeadRanged>();
        public override int BodyID => ModContent.ItemType<HydrothermicArmor>();
        public override int LegsID => ModContent.ItemType<HydrothermicSubligar>();
        public override float KreloadTimeIncreaseValue => 0.24f;
    }
}
