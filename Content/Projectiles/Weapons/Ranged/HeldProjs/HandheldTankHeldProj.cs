using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HandheldTankHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HandheldTank";
        public override int targetCayItem => ModContent.ItemType<HandheldTank>();
        public override int targetCWRItem => ModContent.ItemType<HandheldTankEcType>();
        public override void SetRangedProperty() {
            FireTime = 30;
            kreloadMaxTime = 60;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 60;
            HandDistanceY = 4;
            HandFireDistance = 60;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 25;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            Recoil = 3.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void PostInOwnerUpdate() {
            if (!Owner.PressKey() && kreloadTimeValue == 0) {
                ArmRotSengsFront = 70 * CWRUtils.atoR;
                ArmRotSengsBack = 110 * CWRUtils.atoR;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = CreateRecoil();
        }
    }
}
