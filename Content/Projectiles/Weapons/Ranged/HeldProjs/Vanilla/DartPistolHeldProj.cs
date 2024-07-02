using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DartPistolHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DartPistol].Value;
        public override int targetCayItem => ItemID.DartPistol;
        public override int targetCWRItem => ItemID.DartPistol;
        public override void SetRangedProperty() {
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -2;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            kreloadMaxTime = 30;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void FiringShoot() {
            int damage;
            Item ammoItem = GetSelectedBullets();
            if (ammoItem.type == ItemID.CursedDart) {
                damage = (int)(WeaponDamage * 0.7f);
            }
            else {
                damage = WeaponDamage;

            }
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, damage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].velocity *= 0.6f;
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].timeLeft += 300;
            if (ammoItem.type == ItemID.IchorDart) {
                Main.projectile[proj].aiStyle = 1;
            }
        }
    }
}
