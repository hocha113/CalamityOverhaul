using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTeardropCleaver : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TeardropCleaver>();
        public override int ProtogenesisID => ModContent.ItemType<TeardropCleaverEcType>();
        public override string TargetToolTipItemName => "TeardropCleaverEcType";
        public override void SetDefaults(Item item) => TeardropCleaverEcType.SetDefaultsFunc(item);
    }
}
