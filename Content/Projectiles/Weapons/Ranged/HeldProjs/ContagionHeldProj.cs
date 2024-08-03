using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ContagionHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Contagion";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Contagion>();
        public override int targetCWRItem => ModContent.ItemType<ContagionEcType>();
        public override void SetRangedProperty() {
            CanRightClick = true;
            BowArrowDrawNum = 2;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<NurgleArrow>();
            DrawArrowOffsetRot = MathHelper.Pi;
            CustomDrawOrig = new Vector2(7, 0);
        }
        public override void PostInOwner() {
            if (onFireR) {
                BowArrowDrawBool = false;
                LimitingAngle();
            }
            else {
                BowArrowDrawBool = true;
            }
        }

        public override void BowShoot() {
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, Projectile.Center
                    , UnitToMouseV.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void BowShootR() {
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-520, 520), Main.rand.Next(-732, -623));
                Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13;
                Projectile.NewProjectile(Source, spanPos, vr, ModContent.ProjectileType<NurgleBee>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
