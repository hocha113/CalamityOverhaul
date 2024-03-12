using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class GrenadeLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.GrenadeLauncher].Value;
        public override int targetCayItem => ItemID.GrenadeLauncher;
        public override int targetCWRItem => ItemID.GrenadeLauncher;
        public override void SetRangedProperty() {
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            Recoil = 3.2f;
            RangeOfStress = 5;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override bool KreLoadFulfill() {
            if (BulletNum < 16) {
                BulletNum += 4;
            } else {
                BulletNum = 20;
            }
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
            return true;
        }

        public override void FiringShoot() {
            //火箭弹药特判，榴弹特判
            Item ammoItem = Item.CWR().MagazineContents[0];
            if (ammoItem.type == ItemID.RocketI) {
                AmmoTypes = ProjectileID.GrenadeI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                AmmoTypes = ProjectileID.GrenadeII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                AmmoTypes = ProjectileID.GrenadeIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                AmmoTypes = ProjectileID.GrenadeIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                AmmoTypes = ProjectileID.ClusterGrenadeI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                AmmoTypes = ProjectileID.ClusterGrenadeII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                AmmoTypes = ProjectileID.DryGrenade;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                AmmoTypes = ProjectileID.WetGrenade;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                AmmoTypes = ProjectileID.HoneyGrenade;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                AmmoTypes = ProjectileID.LavaGrenade;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                AmmoTypes = ProjectileID.MiniNukeGrenadeI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                AmmoTypes = ProjectileID.MiniNukeGrenadeII;
            }
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 1.2f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0) ;
        }
    }
}
