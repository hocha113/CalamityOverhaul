using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MidasPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MidasPrime";
        public override int targetCayItem => ModContent.ItemType<MidasPrime>();
        public override int targetCWRItem => ModContent.ItemType<MidasPrimeEcType>();
        bool nextShotGoldCoin = false;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 22;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            CanRightClick = true;
            FiringDefaultSound = false;
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
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            long cashAvailable2 = Utils.CoinsCount(out bool overflow2, Owner.inventory);
            if (cashAvailable2 < 100 && !overflow2) {
                return;
            }
            if (Owner.GetActiveRicoshotCoinCount() >= 4) {
                return;
            }

            SoundEngine.PlaySound(new("CalamityMod/Sounds/Custom/Ultrabling") { PitchVariance = 0.5f }, Projectile.Center);

            long cashAvailable = Utils.CoinsCount(out bool overflow, Owner.inventory);

            if (overflow || cashAvailable > 10000) {
                Owner.BuyItem(10000);
                nextShotGoldCoin = true;
            }
            else {
                Owner.BuyItem(100);
                nextShotGoldCoin = false;
            }

            float coinAIVariable = nextShotGoldCoin ? 2f : 1f;

            Projectile.NewProjectile(Source, GunShootPos, Owner.GetCoinTossVelocity()
                , ModContent.ProjectileType<RicoshotCoin>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, coinAIVariable);

            BulletNum += 1;
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
