using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Shotgun].Value;
        public override int targetCayItem => ItemID.Shotgun;
        public override int targetCWRItem => ItemID.Shotgun;
        public override void SetRangedProperty()
        {
            fireTime = 45;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 2.0f;
            RangeOfStress = 12;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate()
        {
            if (kreloadTimeValue > 0)
            {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void OnKreLoad()
        {
            if (BulletNum < 32)
            {
                BulletNum += 16;
            }

            else
            {
                BulletNum = 48;
            }
        }

        public override void PostFiringShoot()
        {
            if (BulletNum >= 8)
            {
                BulletNum -= 8;
            }
        }

        public override void FiringShoot()
        {
            SpawnGunFireDust();
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            for (int i = 0; i < 7; i++)
            {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.24f, 0.24f)) * Main.rand.NextFloat(0.7f, 1.4f), AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
                _ = CreateRecoil();
            }
        }
    }
}
