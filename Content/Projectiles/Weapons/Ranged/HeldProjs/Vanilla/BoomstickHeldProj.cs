using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BoomstickHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Boomstick].Value;
        public override int targetCayItem => ItemID.Boomstick;
        public override int targetCWRItem => ItemID.Boomstick;
        public override void SetRangedProperty() {
            FireTime = 21;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -6;
            HandDistance = 17;
            HandDistanceY = 4;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 2.0f;
            RangeOfStress = 8;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 30;
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
                    SoundEngine.PlaySound(CWRSound.Gun_Clipout with { Volume = 0.6f }, Projectile.Center);
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
            for (int i = 0; i < 6; i++) {
                int proj = Projectile.NewProjectile(Source2, GunShootPos
                                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(0.7f, 1.4f)
                                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += 0.1f;
                Main.projectile[proj].extraUpdates += 1;
            }
        }
    }
}
