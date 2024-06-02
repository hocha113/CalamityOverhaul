using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PalladiumRepeaterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PalladiumRepeater].Value;
        public override int targetCayItem => ItemID.PalladiumRepeater;
        public override int targetCWRItem => ItemID.PalladiumRepeater;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            DrawCrossArrowToMode = -3;
            IsCrossbow = true;
        }

        public override void FiringShoot() {
            int ammonum = Main.rand.Next(2);
            float angle = Main.rand.NextFloat(0.05f, 0.1f);
            if (ammonum == 0) {
                _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                _ = UpdateConsumeAmmo();

            }
            else {
                for (int i = 0; i < 2; i++) {
                    _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-angle, angle, i))
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    _ = UpdateConsumeAmmo();
                }
            }
        }
    }
}
