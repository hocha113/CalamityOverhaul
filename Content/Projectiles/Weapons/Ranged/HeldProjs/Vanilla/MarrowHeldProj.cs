using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class MarrowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Marrow].Value;
        public override int TargetID => ItemID.Marrow;
        public override void SetRangedProperty() {
            ArmRotSengsBackBaseValue = 70;
            ShootSpanTypeValue = SpanTypesEnum.Marrow;
            ForcedConversionTargetAmmoFunc = () => Owner.IsWoodenAmmo(AmmoTypes);
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ProjectileID.BoneArrowFromMerchant;
            BowstringData.DeductRectangle = new Rectangle(6, 6, 2, 24);
        }
    }
}
