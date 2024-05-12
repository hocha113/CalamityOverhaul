using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class NanoPurgeHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Proj_Magic + "NanoPurgeHoldout";
        public override int targetCayItem => ModContent.ItemType<NanoPurge>();
        public override int targetCWRItem => ModContent.ItemType<NanoPurgeEcType>();
        int fireIndex = 30;
        int fireIndex2;
        int intframe = 15;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 25;
            HandDistanceY = 3;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFire || onFireR) {
                CWRUtils.ClockFrame(ref Projectile.frame, intframe, 3);
            }
            else {
                fireIndex = 30;
                intframe = 15;
            }
        }

        public override void FiringShoot() {
            Item.useTime = fireIndex;
            SoundEngine.PlaySound(SoundID.Item91, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.9f, 1.1f)
                , ModContent.ProjectileType<NanoPurgeLaser>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            if (fireIndex > 5) {
                OffsetPos += ShootVelocity.UnitVector() * -5;
                fireIndex -= 2;
                intframe--;
                if (intframe < 3) {
                    intframe = 3;
                }
            }
        }

        public override void FiringShootR() {
            Item.useTime = 3;
            intframe = 2;
            if (++fireIndex2 >= 2) {
                SoundEngine.PlaySound(SoundID.Item91, Projectile.Center);
                fireIndex2 = 0;
            }
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<NanoPurgeLaser>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void GunDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 4), onFire ? Color.White : lightColor
                , Projectile.rotation + MathHelper.PiOver2, CWRUtils.GetOrig(TextureValue, 4), Projectile.scale, SpriteEffects.None);
        }
    }
}
