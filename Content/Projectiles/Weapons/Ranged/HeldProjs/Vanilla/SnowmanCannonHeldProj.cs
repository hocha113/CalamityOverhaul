using CalamityMod.Tiles.FurnitureVoid;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection.Metadata;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SnowmanCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SnowmanCannon].Value;
        public override int targetCayItem => ItemID.SnowmanCannon;
        public override int targetCWRItem => ItemID.SnowmanCannon;
        public override void SetRangedProperty()
        {
            FireTime = 75;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            Recoil = 4.8f;
            RangeOfStress = 48;
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
            return base.KreLoadFulfill();
        }

        public override void FiringShoot() {
            //火箭弹药特判，且雪人炮特判
            Item ammoItem = Item.CWR().MagazineContents[0];
            if (ammoItem.type == ItemID.RocketI) {
                AmmoTypes = ProjectileID.RocketSnowmanI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                AmmoTypes = ProjectileID.RocketSnowmanII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                AmmoTypes = ProjectileID.RocketSnowmanIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                AmmoTypes = ProjectileID.RocketSnowmanIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                AmmoTypes = ProjectileID.ClusterSnowmanRocketI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                AmmoTypes = ProjectileID.ClusterSnowmanRocketII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                AmmoTypes = ProjectileID.DrySnowmanRocket;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                AmmoTypes = ProjectileID.WetSnowmanRocket;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                AmmoTypes = ProjectileID.HoneySnowmanRocket;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                AmmoTypes = ProjectileID.LavaSnowmanRocket;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                AmmoTypes = ProjectileID.MiniNukeSnowmanRocketI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                AmmoTypes = ProjectileID.MiniNukeSnowmanRocketII;
            }
            SpawnGunFireDust();
            SoundEngine.PlaySound(ScorchedEarth.ShootSound, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 76, dustID2: 149, dustID3: 76);
            _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            for (int i = 0; i < 2; i++) {
                _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, i)) * 0.7f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                _ = UpdateConsumeAmmo();
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }

        public override void PostDraw(Color lightColor) {
            if (BulletNum > 0 && BulletNum <= 4 && IsKreload) {
                string path = CWRConstant.Item_Ranged + "ScorchedEarth_PrimedForAction_" + BulletNum;
                Texture2D value = CWRUtils.GetT2DValue(path);
                Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                    , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
