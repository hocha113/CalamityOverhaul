using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RAcidGun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AcidGun>();
        public override int ProtogenesisID => ModContent.ItemType<AcidGunEcType>();
        public override string TargetToolTipItemName => "AcidGunEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<AcidGunHeldProj>();
    }
}
