using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyStatigel : BaseRangedArmor
    {
        public override int TargetID => "StatigelHeadRanged".GetCalItemID();
        public override int BodyID => "StatigelArmor".GetCalItemID();
        public override int LegsID => "StatigelGreaves".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.12f;
    }
}
