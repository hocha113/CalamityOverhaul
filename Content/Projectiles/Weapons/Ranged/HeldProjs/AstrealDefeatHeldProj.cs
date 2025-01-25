using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstrealDefeatHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstrealDefeat";
        public override int targetCayItem => ModContent.ItemType<AstrealDefeat>();
        public override int targetCWRItem => ModContent.ItemType<AstrealDefeatEcType>();
        public override void SetShootAttribute() {
            FiringDefaultSound = false;
            if (++fireIndex >= 3) {
                FiringDefaultSound = true;
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , ModContent.ProjectileType<AstrealArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, Main.rand.Next(4));
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
