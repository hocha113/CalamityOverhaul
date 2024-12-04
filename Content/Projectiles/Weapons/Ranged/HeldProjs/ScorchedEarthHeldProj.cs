using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
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
            InOwner_HandState_AlwaysSetInFireRoding = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
            EjectCasingProjSize = 3;
            LoadingAA_None.loadingAA_None_Roting = -50;
            LoadingAA_None.loadingAA_None_X = 3;
            LoadingAA_None.loadingAA_None_Y = 0;
        }

        public override void FiringShoot() {
            ModOwner.SetScreenShake(4);
            SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound, Projectile.Center);
            CWRUtils.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<EarthRocketOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }

        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            if (BulletNum > 0 && BulletNum <= 4 && IsKreload) {
                string path = CWRConstant.Item_Ranged + "ScorchedEarth_PrimedForAction_" + BulletNum;
                Texture2D value = CWRUtils.GetT2DValue(path);
                Main.EntitySpriteDraw(value, drawPos, null, onFire ? Color.White : lightColor
                    , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale
                    , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
