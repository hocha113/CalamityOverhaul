using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TitaniumRepeaterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TitaniumRepeater].Value;
        public override int targetCayItem => ItemID.TitaniumRepeater;
        public override int targetCWRItem => ItemID.TitaniumRepeater;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            DrawCrossArrowToMode = -5;
            DrawCrossArrowNum = 3;
            IsCrossbow = true;
        }

        public override void FiringShoot() {
            int ammonum = Main.rand.Next(3);
            if (ammonum == 0) {
                for (int i = 0; i < 2; i++) {
                    _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.07f, 0.07f, i)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    _ = UpdateConsumeAmmo();
                }
            }
            else {
                for (int i = 0; i < 3; i++) {
                    _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.07f, 0.07f, i / 2f)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    _ = UpdateConsumeAmmo();
                }
            }
        }
    }
}
