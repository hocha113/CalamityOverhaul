using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstralRepeaterHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstralRepeater";
        public override int targetCayItem => ModContent.ItemType<AstralRepeater>();
        public override int targetCWRItem => ModContent.ItemType<AstralRepeaterEcType>();
        private int fireIndex;
        private int fireTimeValue;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void PostInOwner() {
            BowArrowDraw = CanFireMotion = true;
            FiringDefaultSound = true;
            if (fireTimeValue > 0) {
                fireTimeValue--;
                BowArrowDraw = CanFireMotion = false;
                FiringDefaultSound = false;
                onFire = false;
            }
        }

        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].extraUpdates = 1;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            fireIndex++;
            if (fireIndex >= 3) {
                fireTimeValue += 15;
                fireIndex = 0;
            }
        }
    }
}
