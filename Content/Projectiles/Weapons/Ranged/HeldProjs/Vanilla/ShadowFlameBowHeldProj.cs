using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ShadowFlameBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ShadowFlameBow].Value;
        public override int targetCayItem => ItemID.ShadowFlameBow;
        public override int targetCWRItem => ItemID.ShadowFlameBow;
        public override void SetRangedProperty() {
            ShootSpanTypeValue = SpanTypesEnum.None;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.WoodenArrowFriendly;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ProjectileID.ShadowFlameArrow;
        }
    }
}
