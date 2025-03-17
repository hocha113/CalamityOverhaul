using CalamityMod.Items.Armor.Daedalus;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyDaedalus : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<DaedalusHeadRanged>();
        public override int BodyID => ModContent.ItemType<DaedalusBreastplate>();
        public override int LegsID => ModContent.ItemType<DaedalusLeggings>();
        public override float KreloadTimeIncreaseValue => 0.2f;
    }
}
