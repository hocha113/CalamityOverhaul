using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClockGatlignumHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClockGatlignum";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ClockGatlignum>();
        public override int targetCWRItem => ModContent.ItemType<ClockGatlignum>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.25f;
            Recoil = 1.5f;
            HandDistance = 15;
            HandDistanceY = 0;
            HandFireDistance = 20;
            HandFireDistanceY = - 3;
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            if (Main.rand.NextBool()) {
                DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, gundir);
                Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
            }

            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3
                    , gundir.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
