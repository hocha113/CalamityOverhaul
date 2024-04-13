using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstrealDefeatHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstrealDefeat";
        public override int targetCayItem => ModContent.ItemType<AstrealDefeat>();
        public override int targetCWRItem => ModContent.ItemType<AstrealDefeatEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void PostInOwner() {
            
        }

        public override void BowShoot() {
            FiringDefaultSound = false;
            if (fireIndex > 3) {
                FiringDefaultSound = true;
                fireIndex = 0;
            }
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , ModContent.ProjectileType<AstrealArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, Main.rand.Next(4));
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            fireIndex++;
        }
    }
}
