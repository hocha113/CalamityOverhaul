using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class TradewindsHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Tradewinds";
        public override int targetCayItem => ModContent.ItemType<Tradewinds>();
        public override int targetCWRItem => ModContent.ItemType<TradewindsEcType>();
        public override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            GunPressure = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            Recoil = 0;
            ArmRotSengsFrontNoFireOffset = 13;
            AngleFirearmRest = 0;
            CanRightClick = true;
        }

        public override void FiringShoot() {
            Item.useTime = 12;
            AmmoTypes = ModContent.ProjectileType<TradewindsProjectile>();
            int proj = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].penetrate = 13;
        }

        public override void FiringShootR() {
            Item.useTime = 25;
            AmmoTypes = ModContent.ProjectileType<Feathers>();
            int proj = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].ai[0] = 2;
            Main.projectile[proj].DamageType = DamageClass.Magic;
            for (int i = 0; i <= 360; i += 3) {
                Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                int num = Dust.NewDust(Owner.Center, Owner.width, Owner.height, DustID.Smoke, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position = Owner.Center + ShootVelocity.UnitVector() * 23;
                Main.dust[num].velocity = vr;
            }
        }
    }
}
