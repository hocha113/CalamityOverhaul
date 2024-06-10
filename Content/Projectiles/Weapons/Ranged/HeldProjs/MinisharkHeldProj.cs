using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MinisharkHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Minishark].Value;
        public override int targetCayItem => ItemID.Minishark;
        public override int targetCWRItem => ItemID.Minishark;//这样的用法可能需要进行一定的考查，因为基类的设计并没有考虑到原版物品
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 7;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 5;
            RepeatedCartridgeChange = true;
            ArmRotSengsBackNoFireOffset = 60;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            SpwanGunDustMngsData.splNum = 0.4f;
            RecoilRetroForceMagnitude = 4;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.02f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
