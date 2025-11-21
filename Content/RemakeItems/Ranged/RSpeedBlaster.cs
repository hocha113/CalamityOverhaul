using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSpeedBlaster : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<SpeedBlasterHeldProj>(80);
        }
    }
}
