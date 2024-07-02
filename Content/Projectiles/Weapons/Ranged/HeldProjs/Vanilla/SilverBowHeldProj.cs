using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SilverBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SilverBow].Value;
        public override int targetCayItem => ItemID.SilverBow;
        public override int targetCWRItem => ItemID.SilverBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.SilverBow;
    }
}
