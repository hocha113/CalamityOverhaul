using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyDaedalus : BaseRangedArmor
    {
        public override int TargetID => "DaedalusHeadRanged".GetCalItemID();
        public override int BodyID => "DaedalusBreastplate".GetCalItemID();
        public override int LegsID => "DaedalusLeggings".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.2f;
    }
}
