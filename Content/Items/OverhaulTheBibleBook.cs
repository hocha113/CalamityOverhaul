using CalamityMod.Items;
using CalamityOverhaul.Content.UIs.OverhaulTheBible;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    internal class OverhaulTheBibleBook : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/OverhaulTheBibleBook_Close";
        private static Asset<Texture2D> OverhaulTheBibleBook_Close;
        private static Asset<Texture2D> OverhaulTheBibleBook_Open_Dark;
        private static Asset<Texture2D> OverhaulTheBibleBook_Open_Light;
        void ICWRLoader.LoadAsset() {
            OverhaulTheBibleBook_Close = CWRUtils.GetT2DAsset(Texture);
            OverhaulTheBibleBook_Open_Dark = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/OverhaulTheBibleBook_Open1");
            OverhaulTheBibleBook_Open_Light = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/OverhaulTheBibleBook_Open2");
        }
        void ICWRLoader.UnLoadData() {
            OverhaulTheBibleBook_Close = null;
            OverhaulTheBibleBook_Open_Dark = null;
            OverhaulTheBibleBook_Open_Light = null;
        }
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
            if (OverhaulTheBibleUI.Instance.Active) {
                TextureAssets.Item[Type] = OverhaulTheBibleBook_Open_Dark;
                spriteBatch.Draw(OverhaulTheBibleBook_Open_Dark.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
                Color sengsColor = Color.White * Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                spriteBatch.Draw(OverhaulTheBibleBook_Open_Light.Value, position, null, sengsColor, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            else {
                TextureAssets.Item[Type] = OverhaulTheBibleBook_Close;
                spriteBatch.Draw(OverhaulTheBibleBook_Close.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void UpdateInventory(Player player) => player.CWR().HasOverhaulTheBibleBook = true;

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
