using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstralRepeaterHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstralBow";
        public override int TargetID => ModContent.ItemType<AstralBow>();
        public override void SetRangedProperty() {
            ShootSpanTypeValue = SpanTypesEnum.AstralRepeater;
            DrawArrowMode = -22;
            BowstringData.DeductRectangle = new Rectangle(4, 8, 2, 62);
        }
        public override void SetShootAttribute() {
            Item.useTime = 4;
            if (++fireIndex >= 3) {
                Item.useTime = 20;
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (fireIndex == 0) {
                Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            }

            Main.projectile[proj].extraUpdates = 1;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
