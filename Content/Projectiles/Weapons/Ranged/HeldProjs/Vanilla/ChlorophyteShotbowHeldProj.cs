using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ChlorophyteShotbowHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ChlorophyteShotbow].Value;
        public override int TargetID => ItemID.ChlorophyteShotbow;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            DrawCrossArrowNum = 3;
            IsCrossbow = true;
            ForcedConversionTargetAmmoFunc = () => Owner.IsWoodenAmmo(AmmoTypes);
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ProjectileID.ChlorophyteArrow;
        }

        public override void FiringShoot() {
            Main.projectile[Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0)].SetArrowRot();
            for (int i = 0; i < 3; i++) {
                Vector2 shootVer = ShootVelocity.RotatedByRandom(0.07f) * Main.rand.NextFloat(0.8f, 1f);
                Main.projectile[Projectile.NewProjectile(Source2, ShootPos, shootVer, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0)].SetArrowRot();
            }
            UpdateConsumeAmmo();
        }
    }
}
