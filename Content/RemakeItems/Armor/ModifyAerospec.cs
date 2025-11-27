using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    //天蓝
    internal class ModifyAerospec : BaseRangedArmor
    {
        public override int TargetID => "AerospecHood".GetCalItemID();
        public override int BodyID => "AerospecBreastplate".GetCalItemID();
        public override int LegsID => "AerospecLeggings".GetCalItemID();
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override float KreloadTimeIncreaseValue => 0.1f;
    }
}
