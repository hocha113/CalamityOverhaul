using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTyrannysEnd : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 1500;
            item.knockBack = 9.5f;
            item.DamageType = DamageClass.Ranged;
            item.useTime = 60;
            item.useAnimation = 60;
            item.shoot = ProjectileID.BulletHighVelocity;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.autoReuse = true;
            item.width = 150;
            item.height = 48;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TyrannysEndHeld>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 5;
            item.CWR().Scope = true;
        }
    }
}
