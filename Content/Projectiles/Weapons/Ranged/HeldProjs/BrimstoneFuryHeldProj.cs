using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BrimstoneFuryHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BrimstoneFury";
        public override int TargetID => ModContent.ItemType<BrimstoneFury>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            HandFireDistanceX = 14;
            BowstringData.DeductRectangle = new Rectangle(2, 6, 2, 52);
        }
        public override void BowShoot() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<BrimstoneBolt>();
            }
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy((-1 + i) * 0.03f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            }
        }
    }
}
