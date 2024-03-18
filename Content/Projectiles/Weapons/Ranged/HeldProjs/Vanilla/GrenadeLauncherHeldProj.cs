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
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
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
            int proj1 = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 1f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0) ;
            Main.projectile[proj1].timeLeft += 120;
            int proj2 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocity + new Vector2(0, -5)) * 0.8f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj2].timeLeft += 120;
            int ammonum = Main.rand.Next(6);
            if (ammonum <= 3) {
                int proj3 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocity + new Vector2(0, -10)) * 0.6f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj3].timeLeft += 120;
            }
            if (ammonum <= 1) {
                int proj4 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocity + new Vector2(0, -15)) * 0.4f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj4].timeLeft += 120;
            }
        }
    }
}
