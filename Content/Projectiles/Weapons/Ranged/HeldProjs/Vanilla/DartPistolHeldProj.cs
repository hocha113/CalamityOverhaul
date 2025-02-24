using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => ItemID.DartPistol;
        public override void SetRangedProperty() {
            FireTime = 18;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -2;
            HandIdleDistanceX = 22;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 22;
            HandFireDistanceY = -2;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.3f;
            RangeOfStress = 48;
            Onehanded = true;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            KreloadMaxTime = 50;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -8;
            if (!MagazineSystem) {
                FireTime += 2;
            }
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].velocity *= 0.6f;
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].timeLeft += 300;
        }
    }
}
