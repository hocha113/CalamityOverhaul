using CalamityMod.Items.Weapons.Typeless;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Typeless
{
    internal class LunicEyeHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "LunicEye";
        public override int targetCayItem => ModContent.ItemType<LunicEye>();
        public override int targetCWRItem => ModContent.ItemType<LunicEyeEcType>();

        public override void SetRangedProperty() {
            HandDistance = 20;
            HandDistanceY = 5;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 24;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RangeOfStress = 25;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<LunicBeam>();
        }
    }
}
