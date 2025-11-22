using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSulphuricAcidCannon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SulphuricAcidCannonHeld>(80);
            item.useAmmo = AmmoID.Bullet;
        }
    }
}
