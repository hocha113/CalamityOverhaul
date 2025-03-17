using CalamityMod.Items.Armor.Tarragon;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyTarragon : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<TarragonHeadRanged>();
        public override int BodyID => ModContent.ItemType<TarragonBreastplate>();
        public override int LegsID => ModContent.ItemType<TarragonLeggings>();
        public override float KreloadTimeIncreaseValue => 0.3f;
    }
}
