using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DrataliornusHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Drataliornus";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Drataliornus>();
        public override int targetCWRItem => ModContent.ItemType<DrataliornusEcType>();

        private int chargeIndex = 35;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandFireDistance = 25;
            DrawArrowMode = -30;
        }

        public override void HandEvent() {
            base.HandEvent();
            if (!onFire && !onFireR) {
                chargeIndex = 35;
            }
            if (onFire) {
                BowArrowDrawNum = 1;
            }
            if (onFireR) {
                BowArrowDrawNum = 5;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                chargeIndex = 0;
            }
        }

        public override void BowShoot() {
            Item.useTime = chargeIndex;
            AmmoTypes = ModContent.ProjectileType<DrataliornusFlame>();
            Vector2 speed = ShootVelocity;
            float ai0 = 0f;
            if (chargeIndex < 6) {
                if (Main.rand.NextBool(2)) {
                    ai0 = 2f;
                    speed /= 2f;
                }
                else {
                    ai0 = 1f;
                }
            }
            FireOffsetPos = ShootVelocity.GetNormalVector() * Main.rand.Next(-20, 20);
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, speed
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, ai0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            chargeIndex--;
            if (chargeIndex < 5) {
                chargeIndex = 5;
            }
        }

        public override void BowShootR() {
            Item.useTime = 40;
            AmmoTypes = ModContent.ProjectileType<DrataliornusFlame>();
            int flameID = ModContent.ProjectileType<DrataliornusFlame>();
            const int numFlames = 5;
            const float fifteenHundredthPi = 0.471238898f;
            Vector2 spinningpoint = ShootVelocity;
            spinningpoint.Normalize();
            spinningpoint *= 36f;
            for (int i = 0; i < numFlames; ++i) {
                float piArrowOffset = i - (numFlames - 1) / 2;
                Vector2 offsetSpawn = spinningpoint.RotatedBy(fifteenHundredthPi * piArrowOffset, new Vector2());
                _ = Projectile.NewProjectile(Source, Projectile.Center + offsetSpawn, ShootVelocity, flameID, WeaponDamage, WeaponKnockback, Owner.whoAmI, 1, 0);
            }
        }
    }
}
