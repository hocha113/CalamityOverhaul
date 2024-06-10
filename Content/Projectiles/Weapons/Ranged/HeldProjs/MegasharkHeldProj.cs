using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegasharkHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Megashark].Value;
        public override int targetCayItem => ItemID.Megashark;
        public override int targetCWRItem => ItemID.Megashark;//这样的用法可能需要进行一定的考查，因为基类的设计并没有考虑到原版物品
        public override void SetRangedProperty() {
            FireTime = 5;
            Recoil = 0.2f;
            GunPressure = 0.1f;
            kreloadMaxTime = 90;
            RangeOfStress = 25;
            HandDistance = 25;
            HandDistanceY = 5;
            ControlForce = 0.05f;
            HandFireDistance = 25;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = 2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustMngsData.splNum = 0.4f;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 3;
            LoadingAA_None.loadingAA_None_Y = 20;
        }
    }
}
