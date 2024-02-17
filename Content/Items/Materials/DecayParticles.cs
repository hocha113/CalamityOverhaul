using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.UIs.SupertableUIs;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DecayParticles : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DecayParticles";
        public new string LocalizationCategory => "Items.Materials";

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 3);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems11;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, Color color, int Type, float alp = 1, float slp = 0) {
            Texture2D value = TextureAssets.Item[Type].Value;
            if (slp == 0) {
                slp = 32 / (float)value.Width;
            }
            spriteBatch.Draw(value, position, null, Color.White * alp, 0, value.Size() / 2, slp, SpriteEffects.None, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawItemIcon(spriteBatch, position, Color.White, Type, slp: 0.5f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, lightColor, Main.GameUpdateCount * 0.1f, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }
    }
}
