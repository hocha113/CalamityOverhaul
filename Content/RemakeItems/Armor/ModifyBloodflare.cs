using CalamityMod.Items.Armor.Bloodflare;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyBloodflare : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<BloodflareHeadRanged>();
        public override int BodyID => ModContent.ItemType<BloodflareBodyArmor>();
        public override int LegsID => ModContent.ItemType<BloodflareCuisses>();
        public override float KreloadTimeIncreaseValue => 0.3f;
    }
}
