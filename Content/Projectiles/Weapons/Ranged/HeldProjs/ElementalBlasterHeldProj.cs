using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ElementalBlasterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ElementalBlaster";
        public override int targetCayItem => ModContent.ItemType<ElementalBlaster>();
        public override int targetCWRItem => ModContent.ItemType<ElementalBlasterEcType>();
        public override void SetRangedProperty() {
            ControlForce = 0f;
            GunPressure = 0f;
            Recoil = 0f;
            HandIdleDistanceX = 20;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                       , ModContent.ProjectileType<EnergyBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source2, Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<EnergyBlast2>(), WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 1, proj, -60);
            Projectile.NewProjectile(Source2, Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<EnergyBlast2>(), WeaponDamage / 2, 0, Owner.whoAmI, -1, proj, 60);
        }
    }
}
