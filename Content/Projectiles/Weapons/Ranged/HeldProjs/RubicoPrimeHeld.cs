using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class RubicoPrimeHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RubicoPrime";
        public override void SetRangedProperty() {
            KreloadMaxTime = 100;
            FireTime = 18;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 22;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -4;
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

        public override void PostInOwner() {
            if (!onFire && IsKreload && kreloadTimeValue <= 0 && Projectile.IsOwnedByLocalPlayer()) {
                if (++fireIndex > 50) {
                    NPC target = Projectile.Center.FindClosestNPC(1900, false, true);
                    if (target != null) {
                        UpdateMagazineContents();
                        Vector2 pos = ShootPos;
                        if (!WeaponHandheldDisplay) {
                            pos = Owner.Center;
                        }
                        SoundEngine.PlaySound("CalamityMod/Sounds/Item/LargeWeaponFire".GetSound()
                            with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
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
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , CWRID.Proj_ImpactRound, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].penetrate = 6;
            fireIndex = 0;
        }
    }
}
