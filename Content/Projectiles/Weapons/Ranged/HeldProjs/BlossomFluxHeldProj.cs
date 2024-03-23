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
        int chargeIndex;
        public void SetCharge() => chargeIndex = 0;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandRotSpeedSengs = 0.6f;
        }

        public override void HandEvent() {
            if (onFire) {
                Item.useTime = 35 - chargeIndex;
                HandRotStartTime = 16 - chargeIndex / 2;
                CanFireMotion = true;
            }
            base.HandEvent();
            if (!onFire && !onFireR) {
                chargeIndex = 0;
            }
        }

        public override void BowShoot() {
            FiringDefaultSound = true;
            AmmoTypes = ModContent.ProjectileType<LeafArrows>();
            base.BowShoot();
            chargeIndex++;
            if (chargeIndex > 32) {
                chargeIndex = 32;
            }
        }

        public override void BowShootR() {
            CanFireMotion = FiringDefaultSound = false;
            Item.useTime = 60;
            AmmoTypes = ModContent.ProjectileType<SporeBombOnSpan>();
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
