using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class RubicoPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RubicoPrime";
        public override int targetCayItem => ModContent.ItemType<RubicoPrime>();
        public override int targetCWRItem => ModContent.ItemType<RubicoPrimeEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 110;
            FireTime = 20;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            if (!onFire && IsKreload) {
                if (++fireIndex > 50) {
                    NPC target = Projectile.Center.FindClosestNPC(1900, false, true);
                    if (target != null) {
                        UpdateMagazineContents();
                        SoundEngine.PlaySound(CommonCalamitySounds.LargeWeaponFireSound with { Volume = CommonCalamitySounds.LargeWeaponFireSound.Volume * 0.45f, Pitch = 0.2f }, Projectile.Center);
                        Vector2 vr = GunShootPos.To(target.Center).UnitVector() * ScaleFactor;
                        Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        SpawnGunFireDust(GunShootPos, vr);
                    }
                    if (BulletNum <= 0) {
                        Item.CWR().IsKreload = false;
                        IsKreload = false;
                        BulletNum = 0;
                    }
                    
                    fireIndex = 0;
                }
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<ImpactRound>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].penetrate = 6;
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
