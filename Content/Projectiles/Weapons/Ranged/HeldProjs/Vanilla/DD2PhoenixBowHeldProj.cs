using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DD2PhoenixBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DD2PhoenixBow].Value;
        public override int TargetID => ItemID.DD2PhoenixBow;
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(6, 12, 2, 32);
        public override void SetShootAttribute() {
            if (++fireIndex > 3) {
                AmmoTypes = ProjectileID.DD2PhoenixBowShot;
                fireIndex = 0;
            }
        }
    }
}
