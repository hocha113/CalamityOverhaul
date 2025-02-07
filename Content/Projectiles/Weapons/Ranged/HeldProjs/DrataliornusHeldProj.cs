using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DrataliornusHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Drataliornus";
        public override int TargetID => ModContent.ItemType<Drataliornus>();
        private int chargeIndex = 35;
        public override void SetRangedProperty() {
            CanRightClick = true;
            HandIdleDistanceX = 18;
            HandFireDistanceX = 28;
            DrawArrowMode = -36;
            BowstringData.DeductRectangle = new Rectangle(6, 10, 2, 70);
        }

        public override void PostInOwner() {
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
                chargeIndex = 35;
                if (!Owner.PressKey(false)) {
                    Owner.wingTime = 0;
                }
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Item.useTime = chargeIndex;
            }
            else if (onFireR) {
                Item.useTime = 40;
                chargeIndex = 35;
            }
            AmmoTypes = ModContent.ProjectileType<DrataliornusFlame>();
        }

        public override void BowShoot() {
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

        }

        public override void PostBowShoot() {
            if (onFire) {
                chargeIndex--;
                if (chargeIndex < 5) {
                    chargeIndex = 5;
                }
            }
        }

        public override void BowShootR() {
            const int numFlames = 5;
            const float fifteenHundredthPi = 0.471238898f;
            Vector2 spinningpoint = ShootVelocity;
            spinningpoint.Normalize();
            spinningpoint *= 36f;
            for (int i = 0; i < numFlames; ++i) {
                float piArrowOffset = i - (numFlames - 1) / 2;
                Vector2 offsetSpawn = spinningpoint.RotatedBy(fifteenHundredthPi * piArrowOffset, new Vector2());
                _ = Projectile.NewProjectile(Source, Projectile.Center + offsetSpawn, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 1, 0);
            }
        }
    }
}
