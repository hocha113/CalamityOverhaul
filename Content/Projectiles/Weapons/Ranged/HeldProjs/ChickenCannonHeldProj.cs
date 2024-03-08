using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ChickenCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ChickenCannon";
        public override int targetCayItem => ModContent.ItemType<ChickenCannon>();
        public override int targetCWRItem => ModContent.ItemType<ChickenCannonEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 20;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            CanRightClick = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            FiringDefaultSound = false;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            RecoilRetroForceMagnitude = 13;
            SoundEngine.PlaySound(SoundID.Item61, Owner.Center);
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            GunPressure = 0;
            ControlForce = 0;
            RecoilRetroForceMagnitude = 0;
            bool spanSound = false;
            for (int i = 0; i < Main.maxProjectiles; ++i) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Owner.whoAmI || p.type != Item.shoot)
                    continue;
                p.timeLeft = 1;
                p.netUpdate = true;
                p.netSpam = 0;
                spanSound = true;
            }
            if (spanSound) {
                RecoilRetroForceMagnitude = 22;
                SoundEngine.PlaySound(SoundID.Item110, Owner.Center);
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
