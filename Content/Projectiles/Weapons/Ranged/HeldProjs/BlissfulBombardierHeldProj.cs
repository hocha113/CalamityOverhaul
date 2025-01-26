using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlissfulBombardierHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlissfulBombardier";
        public override int targetCayItem => ModContent.ItemType<BlissfulBombardier>();
        public override int targetCWRItem => ModContent.ItemType<BlissfulBombardierEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 130;
            FireTime = 12;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.15f;
            ControlForce = 0.03f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            EjectCasingProjSize = 1.4f;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, ModContent.ProjectileType<Nuke>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, AmmoTypes);
        }
    }
}
