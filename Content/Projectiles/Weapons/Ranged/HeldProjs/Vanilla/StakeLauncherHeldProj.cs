using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class StakeLauncherHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.StakeLauncher].Value;
        public override int targetCayItem => ItemID.StakeLauncher;
        public override int targetCWRItem => ItemID.StakeLauncher;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0f;
            IsCrossbow = true;
            DrawCrossArrowNorlMode = 3;
            DrawCrossArrowToMode = -6;
            ForcedConversionTargetAmmoFunc = () => true;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ProjectileID.Stake;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (GunShootCoolingValue == 1) {
                SoundEngine.PlaySound(CWRSound.Ejection with { Volume = 0.5f, Pitch = -1f }, Projectile.Center);
            }
        }

        public override void FiringShoot() {
            base.FiringShoot();
        }
    }
}
