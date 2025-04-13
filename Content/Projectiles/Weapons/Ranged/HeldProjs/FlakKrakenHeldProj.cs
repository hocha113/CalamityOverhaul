﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlakKrakenHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakKraken";
        public override int TargetID => ModContent.ItemType<FlakKraken>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 10;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = -5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -15;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            FiringDefaultSound = false;
            LazyRotationUpdate = true;
            HandheldDisplay = false;
            RecoilRetroForceMagnitude = 17;
            RecoilOffsetRecoverValue = 0.8f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -16;
        }

        public override void HanderCaseEjection() {
            CaseEjection(1.3f);
        }

        public override void SetShootAttribute() {
            FireTime = 8;
            RecoilRetroForceMagnitude = 17 + fireIndex;
            AmmoTypes = ModContent.ProjectileType<FlakKrakenProjectile>();
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocityInProjRot, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ToMouse.Length());
        }

        public override void PostFiringShoot() {
            if (++fireIndex >= 5) {
                FireTime = 50;
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/DudFire")
                    with { Pitch = -0.7f, PitchVariance = 0.1f }, Projectile.Center);
                fireIndex = 0;
                return;
            }
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/FlakKrakenShoot") { Volume = 0.5f }, Projectile.Center);
        }
    }
}
