using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 海豚机枪
    /// </summary>
    internal class SDMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SDMG].Value;
        public override int targetCayItem => ItemID.SDMG;
        public override int targetCWRItem => ItemID.SDMG;
        public override void SetRangedProperty() {
            FireTime = 4;
            ShootPosToMouLengValue = 15;
            ShootPosNorlLengValue = 10;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
            SpwanGunDustMngsData.dustID1 = DustID.PinkStarfish;
            SpwanGunDustMngsData.dustID2 = DustID.PinkStarfish;
            SpwanGunDustMngsData.dustID3 = DustID.PinkStarfish;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
            if (!MagazineSystem) {
                FireTime += 1;
            }
        }
    }
}
