using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RPlasmaRifle : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<PlasmaRifle>();
        public override void SetDefaults(Item item) => item.SetHeldProj<PlasmaRifleHeldProj>();
    }
}
