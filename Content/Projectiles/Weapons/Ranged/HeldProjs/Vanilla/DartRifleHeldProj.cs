using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
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
        public override int TargetID => ItemID.DartRifle;
        public override void SetRangedProperty() {
            FireTime = 35;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -2;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            kreloadMaxTime = 45;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
            if (!MagazineSystem) {
                FireTime += 2;
            }
        }

        public override void FiringShoot() {
            Item ammoItem = ModItem.GetSelectedBullets();
            if (ammoItem.type == ItemID.CursedDart) {
                AmmoTypes = ModContent.ProjectileType<CursedDartRemake>();
            }
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity * 1.5f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0, Main.rand.Next(20));
            Main.projectile[proj].ArmorPenetration += 15;
            Main.projectile[proj].extraUpdates += 1;
        }
    }
}
