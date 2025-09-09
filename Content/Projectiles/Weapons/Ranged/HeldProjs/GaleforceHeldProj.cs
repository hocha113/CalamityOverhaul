using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GaleforceHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Galeforce";
        public override int TargetID => ModContent.ItemType<Galeforce>();
        public override void SetRangedProperty() {
            CanRightClick = true;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 8, 2, 46);
        }
        public override void PostInOwner() {
            Item.useTime = onFireR ? 5 : 20;
        }

        public override void BowShootR() {
            AmmoTypes = ModContent.ProjectileType<FeatherLarge>();
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage / 3 + 2, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].SetArrowRot();
        }

        public override void BowShoot() {
            base.BowShoot();
            for (int i = -8; i <= 8; i += 8) {
                Vector2 perturbedSpeed = ShootVelocity.RotatedBy(MathHelper.ToRadians(i));
                int proj = Projectile.NewProjectile(Source, Projectile.Center, perturbedSpeed
                    , ModContent.ProjectileType<FeatherLarge>(), WeaponDamage / 3, 0f, Owner.whoAmI);
                Main.projectile[proj].SetArrowRot();
            }
        }
    }
}
