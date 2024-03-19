using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
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
        public override int targetCayItem => ModContent.ItemType<Kingsbane>();
        public override int targetCWRItem => ModContent.ItemType<KingsbaneEctype>();
        int upNeedsSengs = 11;
        int chargeSoundSpanTimer = 0;
        int chargeValue;
        int chargeAmmo;
        int thisTime;
        bool thisOnFire;
        public override void SetRangedProperty() {
            kreloadMaxTime = 110;
            FireTime = 1;
            HandDistance = 40;
            HandDistanceY = 5;
            HandFireDistance = 40;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -13;
            ShootPosToMouLengValue = 40;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
            thisTime++;
            if (DownLeft && IsKreload && !Owner.mouseInterface) {
                if (thisTime % 2 == 0)
                    chargeSoundSpanTimer++;
                Projectile.frameCounter++;

                if (Projectile.frameCounter > upNeedsSengs) {
                    if (Projectile.frame == 1 && thisTime < 85) {
                        Projectile.frame = 0;
                    }
                    else
                        Projectile.frame++;
                    if (upNeedsSengs > 0)
                        upNeedsSengs--;
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
            }
            else {
                Projectile.frame = 0;
                thisTime = 0;
                upNeedsSengs = 11;
                thisOnFire = false;
                OffsetPos = Vector2.Zero;
            }

            if (Projectile.frame >= 9) {
                Projectile.frame = 2;
            }
        }

        public override void PostInOwnerUpdate() => onFire = thisOnFire;

        public override void FiringShoot() {
            if (!thisOnFire) {
                return;
            }
            OffsetPos += ShootVelocity.UnitVector() * 3;
            float setPitchVarianceValue = 0.4f;
            if (chargeValue > 90) {
                AmmoTypes = ModContent.ProjectileType<AuricBullet>();
                WeaponDamage *= 2;
                Recoil = 0.5f;
                setPitchVarianceValue = 0.6f;
                ShootPosNorlLengValue = -8;
                chargeAmmo++;
                if (chargeAmmo > 90) {
                    chargeAmmo = chargeValue = 0;
                    Recoil = 0.2f;
                    ShootPosNorlLengValue = -13;
                    SpawnGunFireDust(GunShootPos, ShootVelocity / 2, 13, dustID1: DustID.Smoke, dustID2: DustID.Smoke, dustID3: DustID.Smoke);
                }
            }
            SoundEngine.PlaySound(SoundID.Item40 with { PitchVariance = setPitchVarianceValue }, Projectile.Center);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(MathHelper.ToRadians(4f)) * 3
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            chargeValue++;
        }

        public override void PostFiringShoot() {
            if (!thisOnFire) {
                return;
            }
            base.PostFiringShoot();
            EjectCasing();
        }

        public override bool PreDraw(ref Color lightColor) {
            Color drawColor = onFire ? Color.White : lightColor;
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(TextureValue, Projectile.frame, 10), drawColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 10)
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Item_Ranged + "Kingsbane_barrel");
            Color drawColor2 = CWRUtils.MultiStepColorLerp(chargeValue / 90f, drawColor, Color.Red);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(value, Projectile.frame, 10), drawColor2
                , Projectile.rotation, CWRUtils.GetOrig(value, 10)
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
