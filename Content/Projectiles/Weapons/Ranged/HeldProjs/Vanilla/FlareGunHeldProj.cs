using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 信号枪
    /// </summary>
    internal class FlareGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.FlareGun].Value;
        public override int targetCayItem => ItemID.FlareGun;
        public override int targetCWRItem => ItemID.FlareGun;
        public override void SetRangedProperty() {
            kreloadMaxTime = 32;
            ShootPosToMouLengValue = 10;
            ShootPosNorlLengValue = -6;
            HandDistance = 17;
            HandDistanceY = 0;
            HandFireDistance = 17;
            GunPressure = 0.3f;
            ControlForce = 0.03f;
            Recoil = 1.8f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            SpwanGunDustMngsData.splNum = 0.3f;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            if (!MagazineSystem) {
                LazyRotationUpdate = true;
                FireTime += 12;
            }
        }

        public static int GetFlareDustID(BaseFeederGun gun) {
            int dustType = DustID.Flare;
            Item ammo = gun.GetSelectedBullets();

            if (ammo == null || ammo.type == ItemID.None) {
                return DustID.Flare;
            }

            if (ammo.shoot == ProjectileID.BlueFlare) {
                dustType = DustID.BlueFlare;
            }

            return dustType;
        }

        public override void PostInOwnerUpdate() {
            if (!IsKreload) {
                return;
            }

            int dustType = GetFlareDustID(this);
            int dust = Dust.NewDust(GunShootPos, 1, 1, dustType, ShootVelocity.X, ShootVelocity.Y);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = Main.rand.NextFloat(1, 1.6f);
        }
    }
}
