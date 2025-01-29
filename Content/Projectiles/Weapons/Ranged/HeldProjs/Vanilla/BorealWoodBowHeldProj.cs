using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BorealWoodBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.BorealWoodBow].Value;
        public override int TargetID => ItemID.BorealWoodBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.WoodenBow;
    }
}
