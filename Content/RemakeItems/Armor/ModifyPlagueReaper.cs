using CalamityMod.Items.Armor.Hydrothermic;
using CalamityMod.Items.Armor.PlagueReaper;
using CalamityOverhaul.Content.RemakeItems.Armor.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyPlagueReaper : BaseRangedArmor
    {
        public override int TargetID => ModContent.ItemType<PlagueReaperMask>();
        public override int BodyID => ModContent.ItemType<PlagueReaperVest>();
        public override int LegsID => ModContent.ItemType<PlagueReaperStriders>();
        public override float KreloadTimeIncreaseValue => 0.24f;
    }
}
