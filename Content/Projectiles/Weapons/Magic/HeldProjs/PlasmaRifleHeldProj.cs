using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Sounds;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class PlasmaRifleHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PlasmaRifle";
        public override int targetCayItem => ModContent.ItemType<PlasmaRifle>();
        public override int targetCWRItem => ModContent.ItemType<PlasmaRifleEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -5;
            ShootPosNorlLengValue = -5;
            HandDistance = 15;
            HandDistanceY = 3;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            Recoil = 0;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override int Shoot() {
            SoundEngine.PlaySound(PlasmaRifle.HeavyShotSound, Projectile.Center);
            Item.useTime = 30;
            GunPressure = 0.3f;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<PlasmaShot>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            return 0;
        }

        public override int ShootR() {
            SoundEngine.PlaySound(PlasmaRifle.FastShotSound, Projectile.Center);
            Item.useTime = 10;
            GunPressure = 0.1f;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , ModContent.ProjectileType<PlasmaBolt>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            return 0;
        }

        public override void GunDraw(ref Color lightColor) {
            base.GunDraw(ref lightColor);
        }
    }
}
