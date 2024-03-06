using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ScorchedEarth = CalamityMod.Items.Weapons.Ranged.ScorchedEarth;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ScorpioHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Scorpio";
        public override int targetCayItem => ModContent.ItemType<Scorpio>();
        public override int targetCWRItem => ModContent.ItemType<ScorpioEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 3.2f;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            SoundEngine.PlaySound(ScorchedEarth.ShootSound with { Pitch = 0.3f }, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 28;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<ScorpioOnSpan>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
