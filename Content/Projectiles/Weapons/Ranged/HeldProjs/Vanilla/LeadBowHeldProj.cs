using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class LeadBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.LeadBow].Value;
        public override int targetCayItem => ItemID.LeadBow;
        public override int targetCWRItem => ItemID.LeadBow;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.IronBow;
    }
}
