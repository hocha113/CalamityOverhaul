using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal abstract class AutoItem : ModItem
    {
        public override void AutoStaticDefaults() => AutoStaticDefaults(this);
        public static void AutoStaticDefaults(ModItem item) {
            TextureAssets.Item[item.Item.type] = CWRUtils.GetT2DAsset(item.Texture);

            if (ModContent.RequestIfExists<Texture2D>(item.Texture + "_Flame", out var flameTexture)) {
                TextureAssets.ItemFlame[item.Item.type] = flameTexture;
            }

            item.Item.ResearchUnlockCount = 1;
        }
    }
}
