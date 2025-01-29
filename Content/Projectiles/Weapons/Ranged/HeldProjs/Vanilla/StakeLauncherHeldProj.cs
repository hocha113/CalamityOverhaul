using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class StakeLauncherHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.StakeLauncher].Value;
        public override int TargetID => ItemID.StakeLauncher;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
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

        public override void PostInOwnerUpdate() {
            if (ShootCoolingValue == 1) {
                SoundEngine.PlaySound(CWRSound.Ejection
                    with { Volume = 0.5f, Pitch = -1f }, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            AmmoTypes = ModContent.ProjectileType<StakeHmmod>();
        }
    }
}
