using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyHydrothermic : BaseRangedArmor
    {
        public override int TargetID => "HydrothermicHeadRanged".GetCalItemID();
        public override int BodyID => "HydrothermicArmor".GetCalItemID();
        public override int LegsID => "HydrothermicSubligar".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.24f;
    }
}
