using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PhoenixBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PhoenixBlaster].Value;
        public override int targetCayItem => ItemID.PhoenixBlaster;
        public override int targetCWRItem => ItemID.PhoenixBlaster;
        public override void SetRangedProperty() {
            FireTime = 10;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 8;
            CanRightClick = false;
        }

        public override bool PreOnKreloadEvent() {
            ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
            FeederOffsetRot = -20;
            FeederOffsetPos = new Vector2(DirSign * 6, -6) * SafeGravDir;
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
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_ClipOut with { Volume = 0.65f }, Projectile.Center);
            }
            if (time == 40) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.65f }, Projectile.Center);
            }
            if (time == 10) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.65f }, Projectile.Center);
            }
            return false;
        }

        public override void FiringShoot() {
            FireTime = 10;
            base.FiringShoot();
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 174, dustID2: 213, dustID3: 213);
        }

        public override void FiringShootR() {
            FireTime = 20;
            for (int i = 0; i < 4; i++) {
                SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 174, dustID2: 213, dustID3: 213);
                _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.02f, 0.02f, i / 2f)) * Main.rand.NextFloat(0.7f, 1.5f) * 2f
                    , ModContent.ProjectileType<HellfireBullet>(), WeaponDamage / 3, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
