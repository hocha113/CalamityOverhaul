using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class XenopopperHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Xenopopper].Value;
        public override int TargetID => ItemID.Xenopopper;
        public override void SetRangedProperty() {
            FireTime = 15;
            ControlForce = 0.04f;
            GunPressure = 0.2f;
            Recoil = 0.3f;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 18;
            HandFireDistanceY = -5;
            CanRightClick = true;
            SpwanGunDustMngsData.dustID1 = DustID.Water;
            SpwanGunDustMngsData.dustID2 = DustID.Water;
            SpwanGunDustMngsData.dustID3 = DustID.Water;
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 15;
                Recoil = 1;
                GunPressure = 0.2f;
                EnableRecoilRetroEffect = false;
            }
            else if (onFireR) {
                FireTime = 5;
                Recoil = 0.5f;
                GunPressure = 0.1f;
                EnableRecoilRetroEffect = true;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(0.7f, 1.3f)
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.localAI[0] = AmmoTypes;
                proj.localAI[1] = 11f;
            }
        }

        public override void FiringShootR() {
            Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
