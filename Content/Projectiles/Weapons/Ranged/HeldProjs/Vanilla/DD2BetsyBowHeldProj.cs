using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DD2BetsyBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DD2BetsyBow].Value;
        public override int targetCayItem => ItemID.DD2BetsyBow;
        public override int targetCWRItem => ItemID.DD2BetsyBow;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void BowShoot() {
            AmmoTypes = ProjectileID.DD2BetsyArrow;
            base.BowShoot();
        }
    }
}
