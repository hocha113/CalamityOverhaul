using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class LeviatitanHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Leviatitan";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Leviatitan>();
        public override int targetCWRItem => ModContent.ItemType<LeviatitanEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 100;
            FireTime = 9;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1;
            HandDistance = 27;
            HandDistanceY = 5;
            HandFireDistance = 27;
            HandFireDistanceY = -8;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<AquaBlastToxic>();
                if (Main.rand.NextBool(2)) {
                    AmmoTypes = ModContent.ProjectileType<AquaBlast>();
                }
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
