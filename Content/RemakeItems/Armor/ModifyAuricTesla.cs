using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyAuricTesla : BaseRangedArmor
    {
        public override int TargetID => "AuricTeslaHoodedFacemask".GetCalItemID();
        public override int BodyID => "AuricTeslaBodyArmor".GetCalItemID();
        public override int LegsID => "AuricTeslaCuisses".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.36f;
    }
}
