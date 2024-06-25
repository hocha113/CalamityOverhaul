using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GaleforceHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Galeforce";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Galeforce>();
        public override int targetCWRItem => ModContent.ItemType<GaleforceEcType>();
        public override void SetRangedProperty() => CanRightClick = true;
        public override void PostInOwner() {
            if (onFireR) {
                Item.useTime = 5;
            }
            else {
                Item.useTime = 20;
            }
        }

        public override void BowShootR() {
            AmmoTypes = ModContent.ProjectileType<FeatherLarge>();
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void BowShoot() {
            base.BowShoot();
            for (int i = -8; i <= 8; i += 8) {
                Vector2 perturbedSpeed = ShootVelocity.RotatedBy(MathHelper.ToRadians(i));
                int proj = Projectile.NewProjectile(Source, Projectile.Center, perturbedSpeed, ModContent.ProjectileType<FeatherLarge>(), WeaponDamage, 0f, Owner.whoAmI);
                Main.projectile[proj].SetArrowRot();
            }
        }
    }
}
