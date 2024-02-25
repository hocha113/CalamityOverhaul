using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class BlackMatterStick : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/BlackMatterStick";
        public new string LocalizationCategory => "Items.Materials";

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems5;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, Color color, int Type, float alp = 1) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, position, null, color, 0, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, null, null, null, null, Main.UIScaleMatrix);
            float sngs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.01f));
            for (int i = 0; i < 7; i++) {
                spriteBatch.Draw(TextureAssets.Item[Type].Value, position, null, Color.White * sngs * alp, 0, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            }
            spriteBatch.ResetUICanvasState();
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawItemIcon(spriteBatch, position, Color.White, Type);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, lightColor, Main.GameUpdateCount * 0.1f, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }
    }
}
