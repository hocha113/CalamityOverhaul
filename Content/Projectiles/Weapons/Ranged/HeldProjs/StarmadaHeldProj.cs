using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarmadaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Starmada";
        public override int TargetID => ModContent.ItemType<Starmada>();
        private int maxFireTime = 18;
        private int minFireTime = 8;
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 18;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 24;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 8;
            RecoilOffsetRecoverValue = 0.8f;
            if (!MagazineSystem) {
                minFireTime = 10;
            }
        }

        public override void PostInOwner() {
            if (onFire) {
                if (++fireIndex > 60 && FireTime > minFireTime) {
                    FireTime--;
                    if (FireTime == minFireTime) {
                        SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
                        int maxCount = 36;
                        for (int d = 0; d < maxCount; d++) {
                            Vector2 source = Vector2.Normalize(ShootVelocity) * 9f;
                            source = source.RotatedBy((d - (maxCount / 2 - 1)) * MathHelper.TwoPi / maxCount, default) + Owner.Center;
                            Vector2 dustVel = source - Owner.Center;
                            int index = Dust.NewDust(source + dustVel, 0, 0, DustID.FireworkFountain_Blue, 0f, 0f, 0, default, 4f);
                            Main.dust[index].noGravity = true;
                            Main.dust[index].velocity = dustVel;
                        }
                    }
                    fireIndex = 0;
                }
            }
            else {
                fireIndex = 0;
                FireTime = maxFireTime;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                AmmoTypes = Utils.SelectRandom(Main.rand, [
                            ModContent.ProjectileType<PlasmaBlast>(),
                            ModContent.ProjectileType<AstralStar>(),
                            ModContent.ProjectileType<GalacticaComet>(),
                            ProjectileID.StarCannonStar,
                            ProjectileID.Starfury
                        ]);
                if (AmmoTypes == ProjectileID.Starfury) {
                    WeaponDamage /= 2;
                }
                int proj = Projectile.NewProjectile(Source, ShootPos + CWRUtils.randVr(8), ShootVelocity, AmmoTypes
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].penetrate = 1;
                Main.projectile[proj].timeLeft = 300;
                Main.projectile[proj].DamageType = DamageClass.Ranged;
                Main.projectile[proj].netUpdate = true;
            }
        }
    }
}
