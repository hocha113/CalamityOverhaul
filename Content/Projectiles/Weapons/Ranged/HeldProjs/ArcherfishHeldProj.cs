using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArcherfishHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Archerfish";
        public override int targetCayItem => ModContent.ItemType<Archerfish>();
        public override int targetCWRItem => ModContent.ItemType<ArcherfishEcType>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 30;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0.4f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            SpwanGunDustMngsData.dustID1 = DustID.Water;
            SpwanGunDustMngsData.dustID2 = DustID.Water;
            SpwanGunDustMngsData.dustID3 = DustID.Water;
            SpwanGunDustMngsData.splNum = 0.8f;
            CanCreateCaseEjection = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
        }

        public override void PostFiringShoot() {
            for (int i = 0; i < 4; i++) {
                Gore bubble = Gore.NewGorePerfect(Source, ShootPos, ShootVelocity.RotatedByRandom(MathHelper.ToRadians(30f)) * 0.5f, 411);
                bubble.timeLeft = 6 + Main.rand.Next(4);
                bubble.scale = Main.rand.NextFloat(0.6f, 0.8f);
                bubble.type = Main.rand.NextBool(3) ? 412 : 411;
            }
            FireTime = 3;
            if (++fireIndex > 9) {
                FireTime = 30;
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = Item.shoot;
            }
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source2, ShootPos, ShootVelocity * 1.6f, ModContent.ProjectileType<ArcherfishRing>(), WeaponDamage / 3, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
