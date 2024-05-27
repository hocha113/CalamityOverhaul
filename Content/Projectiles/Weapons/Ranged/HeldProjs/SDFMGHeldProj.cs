using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SDFMGHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SDFMG";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SDFMG>();
        public override int targetCWRItem => ModContent.ItemType<SDFMGEcType>();
        public override void SetRangedProperty() {
            ControlForce = GunPressure = 0;
            Recoil = 0.2f;
        }
        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (Main.rand.NextBool(5)) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity
                , ModContent.ProjectileType<FishronRPG>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
