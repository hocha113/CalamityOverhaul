using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSepticSkewer : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<SepticSkewerProj>();
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<SepticSkewerHeldProj>(8);
        }
    }
}
