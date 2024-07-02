using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlossomFluxHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlossomFlux";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.BlossomFlux>();
        public override int targetCWRItem => ModContent.ItemType<BlossomFluxEcType>();
        private int chargeIndex = 35;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandRotSpeedSengs = 0.6f;
        }

        public override void PostInOwner() {
            if (!onFire && !onFireR) {
                chargeIndex = 35;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                chargeIndex = 35;
                if (!DownRight) {
                    Owner.wingTime = 0;
                }
            }
            BowArrowDrawBool = CanFireMotion = FiringDefaultSound = onFire;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                Item.useTime = 60;
                chargeIndex = 35;
                AmmoTypes = ModContent.ProjectileType<SporeBombOnSpan>();
                return;
            }
            Item.useTime = chargeIndex;
            AmmoTypes = ModContent.ProjectileType<LeafArrows>();
            if (Projectile.IsOwnedByLocalPlayer()) {
                FireOffsetPos = ShootVelocity.GetNormalVector() * Main.rand.Next(-11, 11);
            }
            chargeIndex--;
            if (chargeIndex < 5) {
                chargeIndex = 5;
            }
        }

        public override void BowShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
