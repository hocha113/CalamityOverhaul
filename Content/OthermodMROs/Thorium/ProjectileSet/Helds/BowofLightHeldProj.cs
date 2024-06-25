using CalamityOverhaul.Common;
using CalamityOverhaul.Content.OthermodMROs.Thorium.Core;
using CalamityOverhaul.Content.OthermodMROs.Thorium.ItemSet;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.ProjectileSet.Helds
{
    internal class BowofLightHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[RBowofLight.BowofLightID].Value;
        public override int targetCayItem => RBowofLight.BowofLightID;
        public override int targetCWRItem => RBowofLight.BowofLightID;
        public override bool IsLoadingEnabled(Mod mod) => FromThorium.Has;
    }
}
