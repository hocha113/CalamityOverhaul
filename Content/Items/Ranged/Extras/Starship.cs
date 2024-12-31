using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    /// <summary>
    /// 群星巨舰
    /// </summary>
    internal class Starship : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override void SetDefaults() {
            Item.SetItemCopySD<Infinity>();
            Item.SetCartridgeGun<StarshipHeld>(1300);
        }
    }

    internal class StarshipHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override int targetCayItem => ModContent.ItemType<Starship>();
        public override int targetCWRItem => ModContent.ItemType<Starship>();
        private int upNeedsSengs = 11;
        private int chargeSoundSpanTimer = 0;
        private int chargeValue;
        private int chargeAmmo;
        private int maxChargeValue = 130;
        private int maxChargeAmmo = 90;
        private int thisTime;
        private int fireIndex;
        private bool thisOnFire;
        public override void SetRangedProperty() {
            kreloadMaxTime = 110;
            FireTime = 10;
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 40;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 40;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            HandheldDisplay = false;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {
            thisTime++;
            if (DownLeft && IsKreload && !Owner.CWR().uiMouseInterface) {
                if (thisTime % 2 == 0) {
                    chargeSoundSpanTimer++;
                }
                Projectile.frameCounter++;

                if (Projectile.frameCounter > upNeedsSengs) {
                    if (Projectile.frame == 1 && thisTime < 85) {
                        Projectile.frame = 0;
                    }
                    else {
                        Projectile.frame++;
                    }  
                    if (upNeedsSengs > 0) {
                        upNeedsSengs--;
                    } 
                    Projectile.frameCounter = 0;
                }

                if (thisTime < 90 && chargeSoundSpanTimer > (upNeedsSengs + 2)) {
                    SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (8 - upNeedsSengs) * 0.15f }, Projectile.Center);
                    chargeSoundSpanTimer = 0;
                    if (Projectile.frame > 1) {
                        Projectile.frame = 0;
                    }
                }

                if (thisTime >= 90) {
                    thisOnFire = true;
                }

                Projectile.damage = WeaponDamage * 2;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 1;
            }
            else {
                FireTime = 10;
                Projectile.frame = 0;
                thisTime = 0;
                upNeedsSengs = 11;
                thisOnFire = false;
                OffsetPos = Vector2.Zero;
                if (Time % 3 == 0 && chargeValue > 0) {
                    chargeValue--;
                }
            }

            if (Projectile.frame >= 9) {
                Projectile.frame = 2;
            }

            CanMelee = chargeValue > maxChargeValue;

            if (++fireIndex > 22 && FireTime > 1) {
                FireTime--;
                fireIndex = 0;
            }
        }

        public override void PostInOwnerUpdate() => onFire = thisOnFire;

        public override void HanderSpwanDust() {
            if (chargeValue > maxChargeValue && chargeAmmo > maxChargeAmmo) {
                SpawnGunFireDust(GunShootPos, ShootVelocity / 2, 13
                    , dustID1: DustID.Smoke, dustID2: DustID.Smoke, dustID3: DustID.Smoke);
            }
        }

        public override void FiringShoot() {
            if (!thisOnFire) {
                return;
            }
            float sengs = MathF.Sin(Time * 0.1f) * 0.2f;
            int value = 1;
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(-sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
            if (FireTime == 1) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
            }
            chargeValue++;
        }
    }
}
