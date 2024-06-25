using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SepticSkewerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SepticSkewer";
        public override int targetCayItem => ModContent.ItemType<SepticSkewer>();
        public override int targetCWRItem => ModContent.ItemType<SepticSkewerEcType>();

        public override void SetRangedProperty() {
            FireTime = 1;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0.02f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
            LoadingAA_None.loadingAA_None_Y = 0;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
        }

        public override void PostInOwnerUpdate() {
            FireTime = MagazineSystem ? 1 : 30;
        }

        public override bool PreOnKreloadEvent() {
            ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
            FeederOffsetRot = -20;
            FeederOffsetPos = new Vector2(DirSign * 6, -16) * SafeGravDir;
            Projectile.Center = GetGunBodyPos();
            Projectile.rotation = GetGunBodyRot();
            if (kreloadTimeValue >= 50) {
                ArmRotSengsFront += (kreloadTimeValue - 50) * CWRUtils.atoR * 6;
            }
            if (kreloadTimeValue >= 10 && kreloadTimeValue <= 20) {
                ArmRotSengsFront += (kreloadTimeValue - 10) * CWRUtils.atoR * 6;
            }
            return false;
        }

        public override bool PreReloadEffects(int time, int maxTime) {
            if (time == 50) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_ClipOut with { Volume = 0.75f }, Projectile.Center);
            }
            if (time == 40) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.75f, Pitch = 0.2f }, Projectile.Center);
            }
            if (time == 10) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.75f, Pitch = 0.2f }, Projectile.Center);
            }
            if (time == 1) {
                ShootCoolingValue += 15;
            }
            return false;
        }

        public override void FiringShoot() {
            GunPressure = 0;
            RecoilRetroForceMagnitude = 0;
            if (BulletNum == 7) {
                GunPressure = 0.3f;
                RecoilRetroForceMagnitude = 6;
            }

            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f)
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
