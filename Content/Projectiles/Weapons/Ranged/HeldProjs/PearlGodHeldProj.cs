using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PearlGodHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PearlGod";
        public override int targetCayItem => ModContent.ItemType<PearlGod>();
        public override int targetCWRItem => ModContent.ItemType<PearlGodEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust();
            if (BulletNum <= 1) {
                SpawnGunFireDust(GunShootPos + ShootVelocity, dustID1: DustID.YellowStarDust
                    , dustID2: DustID.FireworkFountain_Blue, dustID3: DustID.FireworkFountain_Blue);
            }
        }

        public override void FiringShoot() {
            if (BulletNum > 1) {
                GunPressure = 0.3f;
                Recoil = 1.2f;
                for (int i = 0; i < 5; i++) {
                    int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy((-2 + i) * (BulletNum * 0.01f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].Calamity().betterLifeBullet1 = true;
                }
            }
            else {
                GunPressure = 1.3f;
                Recoil = 5.2f;
                SoundEngine.PlaySound(CommonCalamitySounds.LargeWeaponFireSound with { Pitch = -0.7f, Volume = 0.7f }, Projectile.Center);
                int proj = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<ShockblastRound>(), WeaponDamage * 5, WeaponKnockback * 2f, Owner.whoAmI, 0f, 10f);
                Main.projectile[proj].extraUpdates += 2;
                Main.projectile[proj].Calamity().betterLifeBullet2 = true;
            }
        }
    }
}
