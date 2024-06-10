using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BloodRainBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.BloodRainBow].Value;
        public override int targetCayItem => ItemID.BloodRainBow;
        public override int targetCWRItem => ItemID.BloodRainBow;
        public override void PostInOwner() {
            if (onFire) {
                LimitingAngle();
            }
        }
        public override void BowShoot() {
            for (int i = 0; i < 2; i++) {
                Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-80, 80), Main.rand.Next(-632, -583));
                Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13;
                Projectile.NewProjectile(Source, spanPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
