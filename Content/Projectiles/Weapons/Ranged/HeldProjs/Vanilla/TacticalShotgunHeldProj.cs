using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TacticalShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TacticalShotgun].Value;
        public override int targetCayItem => ItemID.TacticalShotgun;
        public override int targetCWRItem => ItemID.TacticalShotgun;
        public override void SetRangedProperty() {
            FireTime = 20;
            kreloadMaxTime = 25;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -4;
            HandDistance = 17;
            HandDistanceY = 4;
            GunPressure = 0.3f;
            ControlForce = 0.06f;
            Recoil = 1.4f;
            RangeOfStress = 25;
            FiringDefaultSound = false;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
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
                SoundEngine.PlaySound(CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f}, Projectile.Center);
                if (BulletNum == ModItem.AmmoCapacity) {
                    SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f}, Projectile.Center);
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

        public override void PostFiringShoot() {
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Shoot with { Volume = 0.4f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                int proj = Projectile.NewProjectile(Owner.parent(), GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * Main.rand.NextFloat(1f, 1.5f) * 0.8f
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += 0.3f;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = -1;
                Main.projectile[proj].extraUpdates += 1;
            }
        }
    }
}
