using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPridefulHuntersPlanarRipper : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 45;
            item.SetCartridgeGun<PridefulHuntersPlanarRipperHeldProj>(280);
        }
    }
}
