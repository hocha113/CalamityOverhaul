using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal abstract class AutoItem : ModItem
    {
        public override void AutoStaticDefaults() => AutoStaticDefaults(this);
        public static void AutoStaticDefaults(ModItem item) {
            if (ModContent.HasAsset(item.Texture)) {
                TextureAssets.Item[item.Item.type] = ModContent.Request<Texture2D>(item.Texture);
            }
            else {
                TextureAssets.Item[item.Item.type] = VaultAsset.placeholder3;
            }

            if (ModContent.RequestIfExists<Texture2D>(item.Texture + "_Flame", out var flameTexture)) {
                TextureAssets.ItemFlame[item.Item.type] = flameTexture;
            }

            item.Item.ResearchUnlockCount = 1;
        }
    }
}
