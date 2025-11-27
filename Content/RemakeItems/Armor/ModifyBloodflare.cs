using CalamityOverhaul.Content.RemakeItems.Armor.Core;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyBloodflare : BaseRangedArmor
    {
        public override int TargetID => "BloodflareHeadRanged".GetCalItemID();
        public override int BodyID => "BloodflareBodyArmor".GetCalItemID();
        public override int LegsID => "BloodflareCuisses".GetCalItemID();
        public override float KreloadTimeIncreaseValue => 0.3f;
    }
}
