using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DartRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DartRifle].Value;
        public override int targetCayItem => ItemID.DartRifle;
        public override int targetCWRItem => ItemID.DartRifle;

        public override void SetRangedProperty() {
            FireTime = 35;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -2;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }
        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Item ammoItem = GetSelectedBullets();
            if (ammoItem.type == ItemID.CursedDart) {
                AmmoTypes = ModContent.ProjectileType<CursedDartRemake>();
            }
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 1.5f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0, Main.rand.Next(20));
            Main.projectile[proj].ArmorPenetration += 15;
        }
    }
}
