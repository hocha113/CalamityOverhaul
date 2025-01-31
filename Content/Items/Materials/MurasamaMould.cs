using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class MurasamaMould : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/MurasamaMould";
        public int Durability;
        private const int maxDurability = 5;
        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 3);
            Durability = maxDurability;
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (!(Durability >= maxDurability)) {//这是一个通用的进度条绘制，用于判断耐久进度
                Texture2D barBG = CWRAsset.GenericBarBack.Value;
                Texture2D barFG = CWRAsset.GenericBarFront.Value;
                float barScale = 2f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset) + new Vector2(0, 5);
                float sengs = (1 - (maxDurability - Durability) / (float)maxDurability);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(sengs * barFG.Width), barFG.Height);
                Color color = Color.OrangeRed;
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }
    }
}
