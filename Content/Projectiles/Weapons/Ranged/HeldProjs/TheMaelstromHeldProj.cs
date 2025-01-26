using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheMaelstromHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheMaelstrom";
        public override int targetCayItem => ModContent.ItemType<TheMaelstrom>();
        public override int targetCWRItem => ModContent.ItemType<TheMaelstromEcType>();
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandFireDistance = 26;
            DrawArrowMode = -30;
            BowstringData.DeductRectangle = new Rectangle(14, 18, 4, 102);
        }
        public override void PostInOwner() {
            BowstringData.CanDeduct = BowstringData.CanDraw = onFire;
            BowArrowDrawBool = onFire;
            HandFireDistance = onFire ? 26 : 22;
        }
        public override void SetShootAttribute() {
            if (onFire) {
                Item.UseSound = SoundID.Item5;
                CanFireMotion = FiringDefaultSound = true;
                AmmoTypes = ModContent.ProjectileType<TheMaelstromTheArrow>();
            }
            else if (onFireR) {
                Item.UseSound = SoundID.Item66;
                CanFireMotion = FiringDefaultSound = false;
                AmmoTypes = ModContent.ProjectileType<TheMaelstromSharkOnSpan>();
            }
        }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[AmmoTypes] == 0) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
