using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlossomFluxHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlossomFlux";
        public override int targetCayItem => ModContent.ItemType<BlossomFlux>();
        public override int targetCWRItem => ModContent.ItemType<BlossomFluxEcType>();
        public override void SetRangedProperty() {
            CanRightClick = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            HandRotSpeedSengs = 0.6f;
        }

        public override void PostInOwner() {
            BowArrowDrawBool = CanFireMotion = FiringDefaultSound = onFire;
            Item.useTime = onFireR ? 60 : 4;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                AmmoTypes = ModContent.ProjectileType<SporeBombOnSpan>();
                return;
            }
            AmmoTypes = ModContent.ProjectileType<LeafArrow>();
        }

        public override void BowShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
