using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTelluricGlare : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TelluricGlare>();
        public override int ProtogenesisID => ModContent.ItemType<TelluricGlareEcType>();
        public override string TargetToolTipItemName => "TelluricGlareEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<TelluricGlareHeldProj>();
    }
}
