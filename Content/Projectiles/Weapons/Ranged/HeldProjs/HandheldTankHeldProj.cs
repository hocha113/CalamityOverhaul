using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HandheldTankHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HandheldTank";
        public override int targetCayItem => ModContent.ItemType<HandheldTank>();
        public override int targetCWRItem => ModContent.ItemType<HandheldTankEcType>();
        public override void SetRangedProperty() {
            FireTime = 30;
            kreloadMaxTime = 60;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 60;
            HandDistanceY = 4;
            HandFireDistance = 60;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 25;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            EjectCasingProjSize = 2;
            Recoil = 3.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void PostInOwnerUpdate() {
            if (!DownLeft && kreloadTimeValue == 0) {
                ArmRotSengsFront = 70 * CWRUtils.atoR;
                ArmRotSengsBack = 110 * CWRUtils.atoR;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
