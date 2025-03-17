using CalamityMod.Items.Armor.Auric;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyAuricTesla : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<AuricTeslaHoodedFacemask>();
        public override int BodyID => ModContent.ItemType<AuricTeslaBodyArmor>();
        public override int LegsID => ModContent.ItemType<AuricTeslaCuisses>();
        public override float KreloadTimeIncreaseValue => 0.36f;
    }
}
