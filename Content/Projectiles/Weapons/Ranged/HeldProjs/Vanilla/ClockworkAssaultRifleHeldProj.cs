using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 发条突击步枪
    /// </summary>
    internal class ClockworkAssaultRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ClockworkAssaultRifle].Value;
        public override int TargetID => ItemID.ClockworkAssaultRifle;
        private int chargeAmmoNum;
        public override void SetRangedProperty() {
            FireTime = 3;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 0;
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            Recoil = 0.4f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            KreloadMaxTime = 60;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
            SpwanGunDustMngsData.splNum = 0.3f;
        }

        public override void PostInOwner() { }

        public override void PostFiringShoot() {
            FireTime = 3;
            chargeAmmoNum++;
            if (chargeAmmoNum >= 3) {
                FireTime = 18;
                if (!MagazineSystem) {
                    FireTime += 4;
                }
                chargeAmmoNum = 0;
            }
        }
    }
}
