using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class RocketLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.RocketLauncher].Value;
        public override int targetCayItem => ItemID.RocketLauncher;
        public override int targetCWRItem => ItemID.RocketLauncher;
        public override void SetRangedProperty() {
            FireTime = 60;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            Recoil = 6f;
            RangeOfStress = 10;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
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
            //火箭弹药特判
            Item ammoItem = Item.CWR().MagazineContents[0];
            if (ammoItem.type == ItemID.RocketI) {
                AmmoTypes = ProjectileID.RocketI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                AmmoTypes = ProjectileID.RocketII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                AmmoTypes = ProjectileID.RocketIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                AmmoTypes = ProjectileID.RocketIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                AmmoTypes = ProjectileID.ClusterRocketI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                AmmoTypes = ProjectileID.ClusterRocketII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                AmmoTypes = ProjectileID.DryRocket;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                AmmoTypes = ProjectileID.WetRocket;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                AmmoTypes = ProjectileID.HoneyRocket;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                AmmoTypes = ProjectileID.LavaRocket;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                AmmoTypes = ProjectileID.MiniNukeRocketI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                AmmoTypes = ProjectileID.MiniNukeRocketII;
            }
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            int ammonum1 = Main.rand.Next(6);
            int ammonum2 = Main.rand.Next(6);
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 2;
            Main.projectile[proj].scale *= 1.6f;
            if (ammonum1 <= 2) {
                int proj1 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.03f, 0.03f, ammonum1)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].extraUpdates += 0;
                Main.projectile[proj1].scale *= 1f;
            }
            if (ammonum2 <= 2) {
                int proj2 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.03f, 0.03f, ammonum2)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj2].extraUpdates += 0;
                Main.projectile[proj2].scale *= 1f;
            }
        }
    }
}
