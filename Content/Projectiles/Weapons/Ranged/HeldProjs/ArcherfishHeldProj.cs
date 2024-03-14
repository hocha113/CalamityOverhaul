using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArcherfishHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Archerfish";
        public override int targetCayItem => ModContent.ItemType<Archerfish>();
        public override int targetCWRItem => ModContent.ItemType<ArcherfishEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.4f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                Gore bubble = Gore.NewGorePerfect(Source, GunShootPos, ShootVelocity.RotatedByRandom(MathHelper.ToRadians(30f)) * 0.5f, 411);
                bubble.timeLeft = 6 + Main.rand.Next(4);
                bubble.scale = Main.rand.NextFloat(0.6f, 0.8f);
                bubble.type = Main.rand.NextBool(3) ? 412 : 411;
            }
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = Item.shoot;
            }
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<ArcherfishRing>(), WeaponDamage / 2, WeaponKnockback + 5, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
