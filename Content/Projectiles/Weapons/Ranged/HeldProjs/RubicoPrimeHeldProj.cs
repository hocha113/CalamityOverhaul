using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class RubicoPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RubicoPrime";
        public override int targetCayItem => ModContent.ItemType<RubicoPrime>();
        public override int targetCWRItem => ModContent.ItemType<RubicoPrimeEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 100;
            FireTime = 19;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            AmmoTypeAffectedByMagazine = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
        }

        public override void PostInOwnerUpdate() {
            if (!onFire && IsKreload && kreloadTimeValue <= 0) {
                if (++fireIndex > 50) {
                    NPC target = Projectile.Center.FindClosestNPC(1900, false, true);
                    if (target != null) {
                        UpdateMagazineContents();
                        Vector2 pos = GunShootPos;
                        if (!WeaponHandheldDisplay) {
                            pos = Owner.Center;
                        }
                        SoundEngine.PlaySound(CommonCalamitySounds.LargeWeaponFireSound 
                            with { Volume = CommonCalamitySounds.LargeWeaponFireSound.Volume * 0.45f, Pitch = 0.2f }, Projectile.Center);
                        Vector2 vr = pos.To(target.Center).UnitVector() * ShootSpeedModeFactor;
                        Projectile.NewProjectile(Source, pos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
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
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<ImpactRound>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].penetrate = 6;
        }
    }
}
