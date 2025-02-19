using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
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
        public override int TargetID => ModContent.ItemType<NanoPurge>();
        private int fireIndex2;
        private int intframe = 15;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            CanRightClick = true;
        }

        public override void PostInOwner() {
            if (onFire || onFireR) {
                CWRUtils.ClockFrame(ref Projectile.frame, intframe, 3);
            }
            else {
                fireIndex = 30;
                intframe = 15;
            }
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item91, Projectile.Center);
            }
            else if (onFireR) {
                if (++fireIndex2 >= 2) {
                    SoundEngine.PlaySound(SoundID.Item91, Projectile.Center);
                    fireIndex2 = 0;
                }
            }
        }

        public override void FiringShoot() {
            Item.useTime = fireIndex;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.9f, 1.1f)
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
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<NanoPurgeLaser>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, CWRUtils.GetRec(TextureValue, Projectile.frame, 4), lightColor
                , Projectile.rotation + MathHelper.PiOver2, CWRUtils.GetOrig(TextureValue, 4), Projectile.scale, SpriteEffects.None);
        }
    }
}
