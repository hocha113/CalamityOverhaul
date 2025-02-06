using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DD2BetsyBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DD2BetsyBow].Value;
        public override int TargetID => ItemID.DD2BetsyBow;
        public override void SetRangedProperty() {
            BowArrowDrawNum = 5;
            ForcedConversionTargetAmmoFunc = () => true;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ProjectileID.DD2BetsyArrow;
        }

        public override void BowShoot() {
            for (int i = 0; i < 5; i++) {
                FireOffsetVector = ShootVelocity.RotatedBy((-2 + i) * 0.15f) * 0.2f;
                base.BowShoot();
            }
        }
    }
}
