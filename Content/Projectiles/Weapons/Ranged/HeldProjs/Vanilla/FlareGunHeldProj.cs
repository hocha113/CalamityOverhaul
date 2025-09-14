using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => ItemID.FlareGun;
        public override void SetRangedProperty() {
            KreloadMaxTime = 32;
            ShootPosToMouLengValue = 10;
            ShootPosNorlLengValue = -6;
            HandIdleDistanceX = 17;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 17;
            GunPressure = 0.3f;
            ControlForce = 0.03f;
            Recoil = 1.8f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            SpwanGunDustData.splNum = 0.3f;
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
            Item ammo = gun.Item.CWR().GetSelectedBullets();

            if (ammo == null || ammo.type == ItemID.None) {
                return DustID.Flare;
            }

            if (ammo.shoot == ProjectileID.BlueFlare) {
                dustType = DustID.BlueFlare;
            }

            return dustType;
        }

        public override void PostInOwner() {
            if (!IsKreload || ShootCoolingValue > 0) {
                return;
            }

            int dustType = GetFlareDustID(this);
            Vector2 setTo = ShootVelocity + Owner.velocity;
            int dust = Dust.NewDust(ShootPos, 1, 1, dustType, setTo.X, setTo.Y);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = Main.rand.NextFloat(1, 1.6f);
        }
    }
}
