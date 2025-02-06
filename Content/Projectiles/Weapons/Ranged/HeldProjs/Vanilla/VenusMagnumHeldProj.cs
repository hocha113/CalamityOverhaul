using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 维纳斯万能枪
    /// </summary>
    internal class VenusMagnumHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.VenusMagnum].Value;
        public override int TargetID => ItemID.VenusMagnum;
        public override void SetRangedProperty() {
            FireTime = 7;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -3;
            HandIdleDistanceX = 18;
            HandFireDistanceY = -5;
            HandIdleDistanceY = 0;
            GunPressure = 0.12f;
            ControlForce = 0.05f;
            Recoil = 1.1f;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ProjectileID.BulletHighVelocity;
            SpwanGunDustMngsData.splNum = 0.2f;
            SpwanGunDustMngsData.dustID1 = DustID.FireworkFountain_Yellow;
            SpwanGunDustMngsData.dustID2 = DustID.BoneTorch;
            SpwanGunDustMngsData.dustID3 = DustID.FireworkFountain_Yellow;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
        }
    }
}
