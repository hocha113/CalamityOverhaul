using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ContinentalGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ContinentalGreatbow";
        public override int targetCayItem => ModContent.ItemType<ContinentalGreatbow>();
        public override int targetCWRItem => ModContent.ItemType<ContinentalGreatbowEcType>();
        public override void SetRangedProperty() => BowArrowDrawNum = 3;
        public override void BowShoot() {
            for (int i = 0; i < 2; i++) {
                FireOffsetVector = ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.2f, 0.23f);
                AmmoTypes = Utils.SelectRandom(Main.rand, new int[] { ProjectileID.HellfireArrow, ProjectileID.IchorArrow });
                base.BowShoot();
            }
            bool vms = Main.rand.NextBool(3);
            for (int j = 0; j < 3; j++) {
                FireOffsetPos = ShootVelocity.GetNormalVector() * ((-1 + j) * 15);
                if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                    AmmoTypes = ProjectileID.FireArrow;
                }
                int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = -1;
                if (vms) {
                    Main.projectile[proj].extraUpdates += 1;
                }
            }
        }
    }
}
