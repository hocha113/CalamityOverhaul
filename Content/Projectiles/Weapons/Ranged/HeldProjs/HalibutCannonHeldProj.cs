using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HalibutCannonHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.HalibutCannon>();
        public override int targetCWRItem => ModContent.ItemType<HalibutCannonEcType>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.3f;
            Recoil = 1.7f;
            HandDistance = 40;
            HandDistanceY = 8;
            HandFireDistance = 40;
            HandFireDistanceY = -3;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, gundir);
            if (Main.rand.NextBool()) {
                Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
            }

            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<TorrentialBullet>();
            }

            for (int i = 0; i < 33; i++) {
                int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3
                    , gundir.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)) * Main.rand.NextFloat(0.9f, 1.32f) * 13
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft = 90;
            }
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
