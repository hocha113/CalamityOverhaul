using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class IronBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.IronBow].Value;
        public override int targetCayItem => ItemID.IronBow;
        public override int targetCWRItem => ItemID.IronBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.IronBow;
    }
}
