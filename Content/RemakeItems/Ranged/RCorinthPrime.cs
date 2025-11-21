using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCorinthPrime : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 140;
            item.DamageType = DamageClass.Ranged;
            item.width = 106;
            item.height = 42;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 8f;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<CorinthPrimeHeldProj>(80);
        }
    }
}
