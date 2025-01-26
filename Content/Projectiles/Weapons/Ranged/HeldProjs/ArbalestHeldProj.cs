using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArbalestHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Arbalest";
        public override int targetCayItem => ModContent.ItemType<Arbalest>();
        public override int targetCWRItem => ModContent.ItemType<ArbalestEcType>();
        public override void SetRangedProperty() {
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 30;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 0;
            fireIndex = 1;
            DrawCrossArrowSize = 1;
            DrawCrossArrowNorlMode = 3;
            CanRightClick = true;
            IsCrossbow = true;
        }

        public override void SetShootAttribute() {
            Item.useTime = onFire ? 6 : 8;
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(SoundID.Item5, Owner.Center);
        }

        public override void FiringShoot() {
            int max = fireIndex;
            if (max > 3) {
                max = 3;
            }
            for (int i = 0; i < max; i++) {
                int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.12f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += fireIndex * 0.06f;
                Main.projectile[proj].extraUpdates += 1;
                Main.projectile[proj].velocity *= 1 + fireIndex * 0.05f;
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            }

            _ = UpdateConsumeAmmo();
        }

        public override void PostFiringShoot() {
            if (onFire) {
                if (++fireIndex >= 6) {
                    Item.useTime = 60;
                    fireIndex = 0;
                }
            }
        }

        public override void FiringShootR() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 2;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
            _ = UpdateConsumeAmmo();
        }
    }
}
