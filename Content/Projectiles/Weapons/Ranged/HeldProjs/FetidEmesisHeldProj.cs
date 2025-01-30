using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FetidEmesisHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FetidEmesis";
        public override int TargetID => ModContent.ItemType<FetidEmesis>();
        public override void SetRangedProperty() {
            FireTime = 7;
            ControlForce = 0.06f;
            GunPressure = 0.12f;
            Recoil = 0.75f;
            HandIdleDistanceX = 20;
            HandFireDistanceX = 20;
            HandFireDistanceY = -3;
            ShootPosToMouLengValue = 10;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 5;
            CanRightClick = true;
            SpwanGunDustMngsData.splNum = 0.8f;
            SpwanGunDustMngsData.dustID1 = DustID.GemEmerald;
            SpwanGunDustMngsData.dustID3 = DustID.GemEmerald;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                FireTime = 22;
                GunPressure = 0.25f;
                Recoil = 1.75f;
                return;
            }
            FireTime = 7;
            GunPressure = 0.12f;
            Recoil = 0.75f;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos
                , ShootVelocityInProjRot.RotatedBy(Main.rand.NextFloat(-0.02f, 0.02f))
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.FetidEmesis;

            if (++Projectile.ai[2] > 8) {
                Projectile.NewProjectile(Source2, ShootPos, Vector2.Zero
                , ModContent.ProjectileType<FetidEmesisOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Projectile.ai[2] = 0;
            }
        }

        public override void FiringShootR() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                        , ModContent.ProjectileType<EmesisGore>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].penetrate = 8;
            Main.projectile[proj].extraUpdates += 1;
            for (int i = 0; i < 55; i++) {
                Dust dust = Dust.NewDustDirect(ShootPos, 10, 10, DustID.Shadowflame);
                dust.velocity = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.6f, 1);
                dust.noGravity = true;
            }
        }
    }
}
