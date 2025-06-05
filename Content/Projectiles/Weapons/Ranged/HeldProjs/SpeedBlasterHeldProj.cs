﻿using CalamityMod;
using CalamityMod.Cooldowns;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SpeedBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SpeedBlaster";
        public override int TargetID => ModContent.ItemType<SpeedBlaster>();
        public float DashShotDamageMult = 4.5f;
        public int DashCooldown = 300;
        public float FireRatePowerup = 1.15f;
        public float ColorValue = 0f;
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            CanCreateSpawnGunDust = false;
            RecoilRetroForceMagnitude = 6;
            CanRightClick = true;
            CanUpdateMagazineContentsInShootBool = false;
        }

        public override void SetShootAttribute() {
            FiringDefaultSound = true;
            CanCreateCaseEjection = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            if (onFireR && Owner.HasCooldown(SpeedBlasterBoost.ID)) {
                FiringDefaultSound = false;
                CanCreateCaseEjection = false;
                GunPressure = 0;
                ControlForce = 0;
                SoundEngine.PlaySound(SpeedBlaster.Empty, Owner.Center);
            }
        }

        public override void FiringShoot() {
            Vector2 newVel = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(Owner.HasCooldown(SpeedBlasterBoost.ID) ? 3f : 15f));
            float ShotMode = (Owner.HasCooldown(SpeedBlasterBoost.ID) ? 2f : 0f);
            UpdateMagazineContents();
            Projectile.NewProjectile(Source, ShootPos, newVel, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, ColorValue, ShotMode);
        }

        public override void FiringShootR() {
            if (!Owner.HasCooldown(SpeedBlasterBoost.ID)) {
                if (ColorValue >= 4f) {
                    ColorValue = 0f;
                }
                else {
                    ColorValue += 1f;
                }
                UpdateMagazineContents();
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity, Item.shoot
                    , (int)(WeaponDamage * DashShotDamageMult), WeaponKnockback, Owner.whoAmI, ColorValue, 3f);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.AddCooldown(SpeedBlasterBoost.ID, DashCooldown);
                } 
                Owner.Calamity().sBlasterDashActivated = true;
                if (Owner.velocity != Vector2.Zero) {
                    Color ColorUsed = SpeedBlasterShot.GetColor(ColorValue);
                    for (int i = 0; i <= 8; i++) {
                        GeneralParticleHandler.SpawnParticle(new CritSpark(Owner.Center
                            , Owner.velocity.RotatedByRandom(MathHelper.ToRadians(13f)) * Main.rand.NextFloat(-2.1f, -4.5f)
                            , Color.White, ColorUsed, 2f, 45, 2f, 2.5f));
                    }
                }

                if (ColorValue >= 4f)
                    ColorValue = 0f;
                else
                    ColorValue++;
            }
        }
    }
}
