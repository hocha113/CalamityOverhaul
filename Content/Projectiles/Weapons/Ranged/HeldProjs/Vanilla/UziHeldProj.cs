using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class UziHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Uzi].Value;
        public override int TargetID => ItemID.Uzi;
        public override void SetRangedProperty() {
            FireTime = 6;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -8;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 8;
            HandFireDistanceY = 2;
            GunPressure = 0.15f;
            ControlForce = 0.05f;
            Recoil = 0.22f;
            RepeatedCartridgeChange = true;
            KreloadMaxTime = 45;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ProjectileID.BulletHighVelocity;
            SpwanGunDustData.splNum = 0.3f;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
        }
    }
}
