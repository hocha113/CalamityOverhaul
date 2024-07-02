using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TungstenBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TungstenBow].Value;
        public override int targetCayItem => ItemID.TungstenBow;
        public override int targetCWRItem => ItemID.TungstenBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.SilverBow;
    }
}
