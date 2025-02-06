using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegasharkHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Megashark].Value;
        public override int TargetID => ItemID.Megashark;
        public override void SetRangedProperty() {
            FireTime = 5;
            Recoil = 0.2f;
            GunPressure = 0.1f;
            kreloadMaxTime = 90;
            RangeOfStress = 25;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            ControlForce = 0.05f;
            HandFireDistanceX = 25;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = 2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustMngsData.splNum = 0.4f;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 20;
        }
    }
}
