using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlossomFluxHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlossomFlux";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.BlossomFlux>();
        public override int targetCWRItem => ModContent.ItemType<BlossomFluxEcType>();
        int chargeIndex = 35;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandRotSpeedSengs = 0.6f;
        }

        public override void HandEvent() {
            base.HandEvent();
            if (!onFire && !onFireR) {
                chargeIndex = 35;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                chargeIndex = 35;
            }
        }

        public override void BowShoot() {
            Item.useTime = chargeIndex;
            FiringDefaultSound = true;
            AmmoTypes = ModContent.ProjectileType<LeafArrows>();
            base.BowShoot();
            chargeIndex--;
            if (chargeIndex < 5) {
                chargeIndex = 5;
            }
        }

        public override void BowShootR() {
            FiringDefaultSound = false;
            Item.useTime = 60;
            AmmoTypes = ModContent.ProjectileType<SporeBombOnSpan>();
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
