using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ScorchedEarthHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ScorchedEarth";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ScorchedEarth>();
        public override int targetCWRItem => ModContent.ItemType<ScorchedEarthEcType>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 45;
            HandDistance = 10;
            HandDistanceY = -5;
            HandFireDistance = 0;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 3.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
            EjectCasingProjSize = 3;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(-50, 3, 0);
        }

        public override void FiringShoot() {
            ModOwner.SetScreenShake(4);
            SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<EarthRocketOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }

        public override void GunDraw(ref Color lightColor) {
            base.GunDraw(ref lightColor);
            if (BulletNum > 0 && BulletNum <= 4 && IsKreload) {
                string path = CWRConstant.Item_Ranged + "ScorchedEarth_PrimedForAction_" + BulletNum;
                Texture2D value = CWRUtils.GetT2DValue(path);
                Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                    , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
