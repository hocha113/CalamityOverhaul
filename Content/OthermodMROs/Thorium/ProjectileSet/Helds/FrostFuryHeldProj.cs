using CalamityOverhaul.Content.OthermodMROs.Thorium.Core;
using CalamityOverhaul.Content.OthermodMROs.Thorium.ItemSet;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.ProjectileSet.Helds
{
    internal class FrostFuryHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[RFrostFury.FrostFuryID].Value;
        public override int targetCayItem => RFrostFury.FrostFuryID;
        public override int targetCWRItem => RFrostFury.FrostFuryID;
        public override bool IsLoadingEnabled(Mod mod) => FromThorium.Has;
    }
}
