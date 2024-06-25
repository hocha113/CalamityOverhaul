using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class CopperBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.CopperBow].Value;
        public override int targetCayItem => ItemID.CopperBow;
        public override int targetCWRItem => ItemID.CopperBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.CopperBow;
    }
}
