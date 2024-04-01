using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HandgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Handgun].Value;
        public override int targetCayItem => ItemID.Handgun;
        public override int targetCWRItem => ItemID.Handgun;
        public override void SetRangedProperty() {
            FireTime = 8;
            HandDistance = 16;
            HandDistanceY = -1;
            HandFireDistance = 18;
            HandFireDistanceY = -5;
            ShootPosToMouLengValue = 16;
            ShootPosNorlLengValue = -13;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
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
            base.FiringShoot();
            CaseEjection();
        }
    }
}
