using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSepticSkewer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SepticSkewer>();
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<SepticSkewerProj>();
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<SepticSkewerHeldProj>(8);
        }
    }
}
