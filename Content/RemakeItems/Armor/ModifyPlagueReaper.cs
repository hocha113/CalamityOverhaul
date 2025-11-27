using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyPlagueReaper : BaseRangedArmor
    {
        public override int TargetID => "PlagueReaperMask".GetCalItemID();
        public override int BodyID => "PlagueReaperVest".GetCalItemID();
        public override int LegsID => "PlagueReaperStriders".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.24f;
    }
}
