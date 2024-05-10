using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BladedgeGreatbowHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BladedgeRailbow";
        public override int targetCayItem => ModContent.ItemType<BladedgeRailbow>();
        public override int targetCWRItem => ModContent.ItemType<BladedgeGreatbowEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            HandDistance = 20;
            HandDistanceY = 5;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 25;
            DrawCrossArrowNorlMode = 5;
            DrawCrossArrowNum = 2;
            IsCrossbow = true;
        }

        public override void FiringShoot() {
            Item.useTime = 12;
            for (int i = 0; i < 4; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ProjectileID.Leaf, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].DamageType = DamageClass.Ranged;
            }
            for (int i = 0; i < 2; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.03f) * Main.rand.NextFloat(0.8f, 1), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
                Main.projectile[proj].extraUpdates += 1;
            }
            if (++fireIndex >= 2) {
                Item.useTime = 30;
                fireIndex = 0;
            }
            UpdateConsumeAmmo();
        }
    }
}
