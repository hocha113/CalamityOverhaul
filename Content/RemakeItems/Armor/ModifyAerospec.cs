using CalamityMod.Items.Armor.Aerospec;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    // 天蓝
    internal class ModifyAerospec : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<AerospecHood>();
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override float KreloadTimeIncreaseValue => 0.1f;
        public override int BodyID => ModContent.ItemType<AerospecBreastplate>();
        public override int LegsID => ModContent.ItemType<AerospecLeggings>();
    }
}
