using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TendonBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TendonBow].Value;
        public override int TargetID => ItemID.TendonBow;
        public override void SetRangedProperty() {
            ArmRotSengsBackBaseValue = 70;
            ShootSpanTypeValue = SpanTypesEnum.TendonBow;
            BowstringData.DeductRectangle = new Rectangle(4, 8, 2, 24);
        }
    }
}
