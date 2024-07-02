using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegalodonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Megalodon";
        public override int targetCayItem => ModContent.ItemType<Megalodon>();
        public override int targetCWRItem => ModContent.ItemType<MegalodonEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 4;
            HandDistance = 22;
            HandDistanceY = 5;
            HandFireDistance = 22;
            HandFireDistanceY = -8;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0.06f;
            ControlForce = 0.05f;
            Recoil = 0.22f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustMngsData.splNum = 0.6f;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            if (++fireIndex > 3) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<MiniSharkron>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].MaxUpdates *= 2;
                Main.projectile[proj].Calamity().allProjectilesHome = true;
                fireIndex = 0;
            }
        }
    }
}
