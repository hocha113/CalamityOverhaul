using CalamityMod.Items;
using CalamityOverhaul.Content.UIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    internal class OverhaulTheBibleBook : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/OverhaulTheBibleBook_Close";
        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 48;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = Item.buyPrice(0, 0, 0, 1);
            Item.UseSound = SoundID.Item30 with { Volume = SoundID.Item30.Volume * 0.75f };
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.Book;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Asset<Texture2D> value;
            if (OverhaulTheBibleUI.Instance.Active) {
                value = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/OverhaulTheBibleBook_Open1");
                TextureAssets.Item[Type] = value;
                spriteBatch.Draw(value.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
                Texture2D mengs = CWRUtils.GetT2DValue(CWRConstant.Item + "Tools/OverhaulTheBibleBook_Open2");
                spriteBatch.Draw(mengs, position, null, Color.White * Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f)), 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            else {
                value = CWRUtils.GetT2DAsset(Texture);
                TextureAssets.Item[Type] = value;
                spriteBatch.Draw(value.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void UpdateInventory(Player player) {
            player.CWR().HasOverhaulTheBibleBook = true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                OverhaulTheBibleUI.Instance.Active = !OverhaulTheBibleUI.Instance.Active;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 5)
                .AddIngredient(ItemID.CopperCoin, 5)
                .Register();
        }
    }
}
