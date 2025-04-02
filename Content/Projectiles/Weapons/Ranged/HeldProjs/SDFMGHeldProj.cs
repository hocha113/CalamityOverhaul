using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SDFMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SDFMG";
        public override int TargetID => ModContent.ItemType<SDFMG>();
        private bool onIdle;
        public override void SetRangedProperty() {
            ControlForce = GunPressure = 0;
            Recoil = 0.2f;
            CanCreateSpawnGunDust = false;
        }

        public override void PostInOwner() {
            if (!onFire) {
                onIdle = true;
            }
        }

        public override void HanderPlaySound() {
            if (onFireR) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            else {
                if (++fireIndex > 1 || onIdle) {
                    SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                    fireIndex = 0;
                }
            }
            onIdle = false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (Main.rand.NextBool(4)) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<FishronRPG>(), WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
