using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HalleysInfernoHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalleysInferno";
        public override int targetCayItem => ModContent.ItemType<HalleysInferno>();
        public override int targetCWRItem => ModContent.ItemType<HalleysInfernoEcType>();
        public override void SetRangedProperty() {
            FireTime = 10;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 20;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = -12;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 90;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
