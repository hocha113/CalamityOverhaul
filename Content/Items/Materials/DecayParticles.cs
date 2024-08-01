using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DecayParticles : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DecayParticles";
        public new string LocalizationCategory => "Items.Materials";
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }

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

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 1)
                .AddIngredient(ItemID.SoulofLight, 2)
                .AddIngredient(ItemID.SoulofNight, 2)
                .AddIngredient(ItemID.SoulofFright, 1)
                .AddIngredient(ItemID.SoulofMight, 1)
                .AddIngredient(ItemID.SoulofSight, 1)
                .AddIngredient(ItemID.SoulofFlight, 1)
                .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                    amount = 0;
                })
                .AddOnCraftCallback(CWRRecipes.SpawnAction)
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
