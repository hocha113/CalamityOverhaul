using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheMaelstromHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheMaelstrom";
        public override void SetRangedProperty() {
            CanRightClick = true;
            DrawArrowMode = -30;
            BowstringData.DeductRectangle = new Rectangle(14, 18, 4, 102);
            HandheldDistance *= 0.7f;
        }
        public override void PostInOwner() {
            BowstringData.CanDeduct = BowstringData.CanDraw = onFire;
            Item.UseSound = SoundID.Item5;
            BowArrowDrawBool = CanFireMotion = FiringDefaultSound = true;
            AmmoTypes = ModContent.ProjectileType<TheMaelstromTheArrow>();
            if (onFireR) {
                Item.UseSound = SoundID.Item66;
                BowArrowDrawBool = CanFireMotion = FiringDefaultSound = false;
                AmmoTypes = ModContent.ProjectileType<TheMaelstromSharkOnSpan>();
            }
        }

        public override void SetShootAttribute() { }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[AmmoTypes] == 0) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
