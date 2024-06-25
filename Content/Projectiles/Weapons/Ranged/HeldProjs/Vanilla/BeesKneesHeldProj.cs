using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BeesKneesHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.BeesKnees].Value;
        public override int targetCayItem => ItemID.BeesKnees;
        public override int targetCWRItem => ItemID.BeesKnees;
        public override void SetRangedProperty() => ShootSpanTypeValue = SpanTypesEnum.None;
        public override void HanderPlaySound() => SoundEngine.PlaySound(SoundID.Item97, Projectile.Center);
    }
}
