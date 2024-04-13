using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class GoldBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.GoldBow].Value;
        public override int targetCayItem => ItemID.GoldBow;
        public override int targetCWRItem => ItemID.GoldBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.GoldBow;
    }
}
