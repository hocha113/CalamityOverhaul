using CalamityOverhaul.Common;
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
        public override int targetCayItem => ItemID.Xenopopper;
        public override int targetCWRItem => ItemID.Xenopopper;

        public override void SetRangedProperty() {
            FireTime = 15;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1;
            HandDistance = 18;
            HandDistanceY = 5;
            HandFireDistance = 18;
            HandFireDistanceY = -5;
            CanRightClick = true;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            SpawnGunFireDust(dustID1: DustID.Water, dustID2: DustID.Water, dustID3: DustID.Water);
            FireTime = 15;
            Recoil = 1;
            GunPressure = 0.2f;
            EnableRecoilRetroEffect = false;
            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(0.7f, 1.3f)
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.localAI[0] = AmmoTypes;
                proj.localAI[1] = 11f;
            }
        }

        public override void FiringShootR() {
            SpawnGunFireDust();
            FireTime = 5;
            Recoil = 0.5f;
            GunPressure = 0.1f;
            EnableRecoilRetroEffect = true;
            Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
