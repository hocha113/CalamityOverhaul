using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAnarchyBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AnarchyBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AnarchyBladeEcType>();
        public override string TargetToolTipItemName => "AnarchyBladeEcType";
        public override void SetDefaults(Item item) => AnarchyBladeEcType.SetDefaultsFunc(item);
    }
}
