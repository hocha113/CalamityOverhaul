using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TyrannysEndHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TyrannysEnd";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TyrannysEnd>();
        public override int targetCWRItem => ModContent.ItemType<TyrannysEnd>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 30;
            HandDistance = 45;
            HandDistanceY = 5;
            HandFireDistance = 45;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.5f;
            ControlForce = 0.05f;
            Recoil = 6;
            RangeOfStress = 25;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -23);
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            SpawnGunFireDust();
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
