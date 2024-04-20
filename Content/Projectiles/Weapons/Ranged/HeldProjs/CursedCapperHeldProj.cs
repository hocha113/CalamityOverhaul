using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CursedCapperHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CursedCapper";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CursedCapper>();
        public override int targetCWRItem => ModContent.ItemType<CursedCapperEcType>();
        public override void SetRangedProperty() {
            FireTime = 8;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1.2f;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -8;
        }

        public override bool PreOnKreloadEvent() {
            ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
            FeederOffsetRot = -20;
            FeederOffsetPos = new Vector2(DirSign * 1, -16) * SafeGravDir;
            Projectile.Center = GetGunBodyPostion();
            Projectile.rotation = GetGunBodyRotation();
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
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.75f }, Projectile.Center);
            }
            if (time == 10) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.75f }, Projectile.Center);
            }
            return false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, ProjectileID.CursedBullet, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CaseEjection();
        }
    }
}
