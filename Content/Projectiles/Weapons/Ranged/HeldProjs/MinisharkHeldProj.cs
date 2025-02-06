using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => ItemID.Minishark;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 7;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
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
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.02f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
