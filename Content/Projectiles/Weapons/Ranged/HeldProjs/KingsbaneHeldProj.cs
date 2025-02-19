using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class KingsbaneHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "KingsbaneWindUp";
        public override int TargetID => ModContent.ItemType<Kingsbane>();
        private int upNeedsSengs = 11;
        private int chargeSoundSpanTimer = 0;
        private int chargeValue;
        private int chargeAmmo;
        private int maxChargeValue = 130;
        private int maxChargeAmmo = 90;
        private int thisTime;
        private bool thisOnFire;
        public override void SetRangedProperty() {
            kreloadMaxTime = 110;
            FireTime = 1;
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 40;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 40;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
        }

        public override void PreInOwner() {
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
        }

        public override void PostInOwner() => onFire = thisOnFire;

        public override void HanderSpwanDust() {
            if (chargeValue > maxChargeValue && chargeAmmo > maxChargeAmmo) {
                SpawnGunFireDust(ShootPos, ShootVelocity / 2, 13
                    , dustID1: DustID.Smoke, dustID2: DustID.Smoke, dustID3: DustID.Smoke);
            }
        }

        public override void FiringShoot() {
            if (!thisOnFire) {
                return;
            }
            OffsetPos += ShootVelocity.UnitVector() * 3;
            float setPitchVarianceValue = 0.4f;
            if (chargeValue > maxChargeValue) {
                AmmoTypes = ModContent.ProjectileType<AuricBullet>();
                WeaponDamage *= 2;
                Recoil = 0.5f;
                setPitchVarianceValue = 0.6f;
                chargeAmmo++;
                if (chargeAmmo > maxChargeAmmo) {
                    chargeAmmo = chargeValue = 0;
                    Recoil = 0.2f;
                }
            }
            SoundEngine.PlaySound(SoundID.Item40 with { PitchVariance = setPitchVarianceValue }, Projectile.Center);
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(MathHelper.ToRadians(4f)) * 3
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            chargeValue++;
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Color drawColor = lightColor;
            Main.EntitySpriteDraw(TextureValue, drawPos
                , CWRUtils.GetRec(TextureValue, Projectile.frame, 10), drawColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 10)
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Item_Ranged + "Kingsbane_barrel");
            Color drawColor2 = VaultUtils.MultiStepColorLerp(chargeValue / (float)maxChargeValue, drawColor, Color.Red);
            Main.EntitySpriteDraw(value, drawPos
                , CWRUtils.GetRec(value, Projectile.frame, 10), drawColor2
                , Projectile.rotation, CWRUtils.GetOrig(value, 10)
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
