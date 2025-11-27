using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyGodSlayer : BaseRangedArmor
    {
        public override int TargetID => "GodSlayerHeadRanged".GetCalItemID();
        public override int BodyID => "GodSlayerChestplate".GetCalItemID();
        public override int LegsID => "GodSlayerLeggings".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.32f;
    }
}
