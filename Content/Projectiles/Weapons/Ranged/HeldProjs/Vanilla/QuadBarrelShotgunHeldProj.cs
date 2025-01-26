using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 四管霰弹枪
    /// </summary>
    internal class QuadBarrelShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.QuadBarrelShotgun].Value;
        public override int targetCayItem => ItemID.QuadBarrelShotgun;
        public override int targetCWRItem => ItemID.QuadBarrelShotgun;
        public override void SetRangedProperty() {
            FireTime = 25;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 17;
            HandIdleDistanceY = 4;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 2.8f;
            RangeOfStress = 10;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 20;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.loadShellSound = CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f };
            LoadingAA_Shotgun.pump = CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f, Pitch = -0.3f };
            LoadingAA_Shotgun.pumpCoolingValue = 15;
            if (!MagazineSystem) {
                FireTime += kreloadMaxTime;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 8; i++) {
                _ = Projectile.NewProjectile(Source, ShootPos
                    , ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.8f, 1.2f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            }
        }
    }
}
