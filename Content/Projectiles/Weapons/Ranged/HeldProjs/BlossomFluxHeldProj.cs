using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlossomFluxHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlossomFlux";
        public override int TargetID => ModContent.ItemType<BlossomFlux>();
        private bool onIdle;
        public override void SetRangedProperty() {
            CanRightClick = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            HandRotSpeedSengs = 0.6f;
            HandFireDistanceX = 14;
            BowstringData.DeductRectangle = new Rectangle(8, 20, 2, 28);
        }

        public override void PostInOwner() {
            BowArrowDrawBool = CanFireMotion = FiringDefaultSound = onFire;
            Item.useTime = onFireR ? 40 : 4;
            if (onFire) {
                BowstringData.CanDraw = true;
                BowstringData.CanDeduct = true;
            }
            else {
                onIdle = true;
                BowstringData.CanDraw = false;
                BowstringData.CanDeduct = false;
            }
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                AmmoTypes = ModContent.ProjectileType<SporeBombOnSpan>();
                return;
            }
            AmmoTypes = ModContent.ProjectileType<LeafArrow>();
        }

        public override void HanderPlaySound() {
            if (onFireR) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            else {
                if (++fireIndex > 3 || onIdle) {
                    SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                    fireIndex = 0;
                }
            }
            onIdle = false;
        }

        public override void BowShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.identity);
        }
    }
}
