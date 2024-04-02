using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class OnyxBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.OnyxBlaster].Value;
        public override int targetCayItem => ItemID.OnyxBlaster;
        public override int targetCWRItem => ItemID.OnyxBlaster;
        public override void SetRangedProperty() {
            FireTime = 36;
            kreloadMaxTime = 18;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.1f;
            Recoil = 6f;
            RangeOfStress = 48;
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
                SoundEngine.PlaySound(CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f }, Projectile.Center);
                if (BulletNum == ModItem.AmmoCapacity) {
                    SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f }, Projectile.Center);
                    GunShootCoolingValue += 35;
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
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Shoot2 with { Volume = 0.4f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.13f) * Main.rand.NextFloat(0.9f, 1.5f)
                    , ProjectileID.BlackBolt, (int)(WeaponDamage * 0.9f), WeaponKnockback, Owner.whoAmI, 0);
                int proj2 = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedByRandom(0.11f) * Main.rand.NextFloat(0.9f, 1.2f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft += Main.rand.Next(30);
                Main.projectile[proj2].extraUpdates += 1;
                Main.projectile[proj2].scale += Main.rand.NextFloat(0.5f);
            }
        }
    }
}
