using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SDFMGHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SDFMG";
        public override int TargetID => ModContent.ItemType<SDFMG>();
        public override void SetRangedProperty() {
            ControlForce = GunPressure = 0;
            Recoil = 0.2f;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (Main.rand.NextBool(5)) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<FishronRPG>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
