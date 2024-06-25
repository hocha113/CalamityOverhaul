using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class AshWoodBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.AshWoodBow].Value;
        public override int targetCayItem => ItemID.AshWoodBow;
        public override int targetCWRItem => ItemID.AshWoodBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.WoodenBow;
    }
}
