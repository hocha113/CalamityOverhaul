using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class MusketHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Musket].Value;
        public override int targetCayItem => ItemID.Musket;
        public override int targetCWRItem => ItemID.Musket;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 20;
            HandDistanceY = 0;
            HandFireDistance = 16;
            GunPressure = 0.6f;
            ControlForce = 0.1f;
            Recoil = 2.8f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            NO_EEMONG_LOADINGNONESET = false;
            InOwner_HandState__AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            if (!MagazineSystem) {
                FireTime += 36;
            }
        }

        public override bool PreOnKreloadEvent() {
            ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir + 0.3f;
            if (kreloadTimeValue >= 50) {
                ArmRotSengsFront += (kreloadTimeValue - 50) * CWRUtils.atoR * 6 * SafeGravDir;
            }
            if (kreloadTimeValue >= 10 && kreloadTimeValue <= 20) {
                ArmRotSengsFront += (kreloadTimeValue - 10) * CWRUtils.atoR * 6 * SafeGravDir;
            }
            return false;
        }

        public override bool PreReloadEffects(int time, int maxTime) {
            if (time == 50) {
                CaseEjection();
                SoundEngine.PlaySound(CWRSound.Gun_Musket_ClipOut with { Volume = 0.65f }, Projectile.Center);
            }
            if (time == 10) {
                SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot with { Pitch = -0.3f, Volume = 0.55f }, Projectile.Center);
            }
            if (time == 1) {//最好不要这么做，这是不规范的
                ShootCoolingValue = 10;
                ModItem.NoKreLoadTime = 35;
            }
            return false;
        }
    }
}
