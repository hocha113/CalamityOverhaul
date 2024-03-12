using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework.Input;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StormDragoonHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormDragoon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.StormDragoon>();
        public override int targetCWRItem => ModContent.ItemType<StormDragoon>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.3f;
            Recoil = 0.45f;
            HandFireDistanceY = -7;
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, gundir);
            if (Main.rand.NextBool()) {
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
