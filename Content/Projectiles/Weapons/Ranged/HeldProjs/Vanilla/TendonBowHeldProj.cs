using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TendonBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TendonBow].Value;
        public override int targetCayItem => ItemID.TendonBow;
        public override int targetCWRItem => ItemID.TendonBow;
        public override void SetRangedProperty() {
            ArmRotSengsBackBaseValue = 70;
            ShootSpanTypeValue = SpanTypesEnum.TendonBow;
        }

        public override void PostInOwner() {
            base.PostInOwner();
        }

        public override void BowShoot() {
            base.BowShoot();
        }
    }
}
