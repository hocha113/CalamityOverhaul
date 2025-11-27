using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyTarragon : BaseRangedArmor
    {
        public override int TargetID => "TarragonHeadRanged".GetCalItemID();
        public override int BodyID => "TarragonBreastplate".GetCalItemID();
        public override int LegsID => "TarragonLeggings".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.3f;
    }
}
