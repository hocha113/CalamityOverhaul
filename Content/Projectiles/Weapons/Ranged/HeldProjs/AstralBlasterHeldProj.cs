using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstralBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstralBlaster";
        public override int targetCayItem => ModContent.ItemType<AstralBlaster>();
        public override int targetCWRItem => ModContent.ItemType<AstralBlasterEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceY = 6;
            HandFireDistanceX = 20;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 12;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<AstralRound>();
            SpwanGunDustMngsData.splNum = 0.3f;
            SpwanGunDustMngsData.dustID1 = DustID.RedStarfish;
            SpwanGunDustMngsData.dustID2 = DustID.YellowStarDust;
        }
    }
}
