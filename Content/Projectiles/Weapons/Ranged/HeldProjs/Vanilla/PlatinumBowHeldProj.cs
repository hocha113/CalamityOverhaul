using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PlatinumBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PlatinumBow].Value;
        public override int TargetID => ItemID.PlatinumBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.GoldBow;
    }
}
