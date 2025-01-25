using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class LunarianBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "LunarianBow";
        public override int targetCayItem => ModContent.ItemType<LunarianBow>();
        public override int targetCWRItem => ModContent.ItemType<LunarianBowEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            fireIndex = 0;
        }
        public override void SetShootAttribute() {
            Item.useTime = 10;
            if (++fireIndex >= 5) {
                Item.useTime = 50;
                fireIndex = 0;
            }
            //如果这些开发者愿意遵守那该死的开发手册，就不会需要多写这么多该死特判代码
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<LunarBolt>();
            }
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.GetNormalVector() * 3, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            int proj2 = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.GetNormalVector() * -3, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj2].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj2].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
