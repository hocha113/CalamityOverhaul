using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CleansingBlazeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CleansingBlaze";
        public override int targetCayItem => ModContent.ItemType<CleansingBlaze>();
        public override int targetCWRItem => ModContent.ItemType<CleansingBlazeEcType>();
        public override void SetRangedProperty() {
            FireTime = 3;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
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
            int num6 = Main.rand.Next(2, 4);
            for (int index = 0; index < num6; ++index) {
                float SpeedX = ShootVelocity.X + (Main.rand.Next(-15, 16) * 0.05f);
                float SpeedY = ShootVelocity.Y + (Main.rand.Next(-15, 16) * 0.05f);
                _ = Projectile.NewProjectile(Source, GunShootPos.X, GunShootPos.Y, SpeedX, SpeedY, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
            }
        }
    }
}
