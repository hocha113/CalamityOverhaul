using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BulletFilledShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BulletFilledShotgun";
        public override int targetCayItem => ModContent.ItemType<BulletFilledShotgun>();
        public override int targetCWRItem => ModContent.ItemType<BulletFilledShotgunEcType>();
        public override void SetRangedProperty() {
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 28;
            kreloadMaxTime = 20;
            RepeatedCartridgeChange = true;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 3, 15);
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
            int bulletAmt = Main.rand.Next(25, 35);
            for (int i = 0; i < bulletAmt; i++) {
                float newSpeedX = ShootVelocity.X + Main.rand.NextFloat(-15f, 15f);
                float newSpeedY = ShootVelocity.Y + Main.rand.NextFloat(-15f, 15f);
                int proj = Projectile.NewProjectile(Source, GunShootPos, new Vector2(newSpeedX, newSpeedY), Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                Main.projectile[proj].extraUpdates += 1;
            }
            _ = CreateRecoil();
        }
    }
}
