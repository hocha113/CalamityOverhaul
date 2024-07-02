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
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 20;
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
