using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class P90HeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "P90";
        public override int targetCayItem => ModContent.ItemType<P90>();
        public override int targetCWRItem => ModContent.ItemType<P90EcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 2;
            HandDistanceY = 0;
            HandDistance = HandFireDistance = 12;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            Recoil = GunPressure = ControlForce = 0;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<P90Round>();
            LoadingAA_None.loadingAA_None_Roting = 20;
            LoadingAA_None.loadingAA_None_X = 3;
            LoadingAA_None.loadingAA_None_Y = 5;
            CanCreateSpawnGunDust = false;
        }
    }
}
