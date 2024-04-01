using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Shotgun].Value;
        public override int targetCayItem => ItemID.Shotgun;
        public override int targetCWRItem => ItemID.Shotgun;
        public override void SetRangedProperty() {
            FireTime = 20;
            kreloadMaxTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -4;
            HandDistance = 17;
            HandDistanceY = 4;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 2.4f;
            RangeOfStress = 12;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool PreConsumeAmmoEvent() {
            return false;
        }

        public override bool PreReloadEffects(int time, int maxTime) {
            if (time == 1) {
                if (BulletNum == ModItem.AmmoCapacity) {
                    SoundEngine.PlaySound(CWRSound.Gun_Clipout with { Volume = 0.65f, Pitch = 0.2f }, Projectile.Center);
                    GunShootCoolingValue += 15;
                }
                else {
                    
                    SoundEngine.PlaySound(CWRSound.Gun_Clipin with { Volume = 0.65f, Pitch = 0.2f }, Projectile.Center);
                }
            }
            if (time == 5 && BulletNum == ModItem.AmmoCapacity) {
                SoundEngine.PlaySound(CWRSound.Gun_ClipinLocked with { Volume = 0.6f }, Projectile.Center);
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
            SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Shoot with { Volume = 0.35f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                int proj = Projectile.NewProjectile(Owner.parent(), GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.24f, 0.24f)) * Main.rand.NextFloat(0.7f, 1.4f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += 0.1f;
            }
        }
    }
}
