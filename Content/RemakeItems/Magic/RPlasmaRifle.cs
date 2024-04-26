using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RPlasmaRifle : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PlasmaRifle>();
        public override int ProtogenesisID => ModContent.ItemType<PlasmaRifleEcType>();
        public override string TargetToolTipItemName => "PlasmaRifleEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<PlasmaRifleHeldProj>();
    }
}
