using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarfleetHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Starfleet";
        public override int targetCayItem => ModContent.ItemType<Starfleet>();
        public override int targetCWRItem => ModContent.ItemType<StarfleetEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 10;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            CanCreateCaseEjection = false;
        }

        public override void FiringShoot() {
            for (int index = 0; index < 3; ++index) {
                Vector2 velocity = ShootVelocity;
                velocity.X += Main.rand.Next(-40, 41) * 0.05f;
                velocity.Y += Main.rand.Next(-40, 41) * 0.05f;
                int proj = Projectile.NewProjectile(Source, GunShootPos, velocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = -1;
                Main.projectile[proj].scale += Main.rand.NextFloat(0.2f);
                if (Main.rand.NextBool(3)) {
                    Main.projectile[proj].extraUpdates += 1;
                }
            }
        }
    }
}
