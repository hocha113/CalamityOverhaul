using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShroomerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shroomer";
        public override int targetCayItem => ModContent.ItemType<Shroomer>();
        public override int targetCWRItem => ModContent.ItemType<ShroomerEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 26;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
        }

        public override void FiringShoot() {
            base.FiringShoot();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<Shroom>(), (int)(WeaponDamage * 0.7f), WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
