using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HalleysInfernoHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalleysInferno";
        public override int targetCayItem => ModContent.ItemType<HalleysInferno>();
        public override int targetCWRItem => ModContent.ItemType<HalleysInfernoEcType>();
        public override void SetRangedProperty() {
            FireTime = 10;
            HandDistance = 20;
            HandDistanceY = 4;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = CreateRecoil();
        }
    }
}
