using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FungicideHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Fungicide";
        public override int targetCayItem => ModContent.ItemType<Fungicide>();
        public override int targetCWRItem => ModContent.ItemType<FungicideEcType>();
        public override void SetRangedProperty() {
            fireTime = 30;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            HandFireDistance = 15;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust(dustID1: DustID.BlueFairy, dustID2: DustID.BlueFairy, dustID3: DustID.BlueFairy);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, heldItem.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = CreateRecoil();
        }
    }
}
