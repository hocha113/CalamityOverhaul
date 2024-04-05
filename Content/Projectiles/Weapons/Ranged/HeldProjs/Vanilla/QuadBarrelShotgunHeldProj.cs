using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 四管霰弹枪
    /// </summary>
    internal class QuadBarrelShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.QuadBarrelShotgun].Value;
        public override int targetCayItem => ItemID.QuadBarrelShotgun;
        public override int targetCWRItem => ItemID.QuadBarrelShotgun;
        public override void SetRangedProperty() {
            FireTime = 55;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 2.8f;
            RangeOfStress = 10;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 20;
            LoadingQuantity = 1;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool PreConsumeAmmoEvent() {
            return false;
        }

        public override bool PreReloadEffects(int time, int maxTime) {
            if (time == 1) {
                SoundEngine.PlaySound(CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f }, Projectile.Center);
                if (BulletNum == ModItem.AmmoCapacity) {
                    SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
                    GunShootCoolingValue += 15;
                }
            }
            return false;
        }

        public override bool KreLoadFulfill() {
            if (BulletNum < ModItem.AmmoCapacity) {
                if (BulletNum == 0) {
                    BulletReturn();
                    LoadingQuantity = 0;
                    LoadBulletsIntoMagazine();
                    LoadingQuantity = 1;
                }
                ExpendedAmmunition();
                OnKreload = true;
                kreloadTimeValue = kreloadMaxTime;
            }
            return true;
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            for (int i = 0; i < 8; i++) {
                _ = Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.8f, 1.2f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            }
        }
    }
}
