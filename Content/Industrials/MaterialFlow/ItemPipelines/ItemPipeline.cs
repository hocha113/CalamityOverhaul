using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 物流管道物品
    /// </summary>
    internal class ItemPipeline : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ItemPipelineItem";
        public override int CreateTileID => ModContent.TileType<ItemPipelineTile>();

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Green;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe(333)
                    .AddRecipeGroup(RecipeGroupID.IronBar, 5)
                    .AddIngredient(ItemID.Chest, 2)
                    .AddIngredient(ItemID.Glass, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
                return;
            }
            CreateRecipe(333)
                .AddIngredient(CWRID.Item_DubiousPlating, 5)
                .AddIngredient(CWRID.Item_MysteriousCircuitry, 5)
                .AddRecipeGroup(RecipeGroupID.IronBar, 5)
                .AddIngredient(ItemID.Chest, 2)
                .AddIngredient(ItemID.Glass, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 物流管道物块
    /// </summary>
    internal class ItemPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            AddMapEntry(new Color(180, 140, 90), VaultUtils.GetLocalizedItemName<ItemPipeline>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Copper);
            return false;
        }

        public override bool RightClick(int i, int j) {
            if (Main.LocalPlayer.GetItem().type == ModContent.ItemType<ItemPipeline>()) {
                return false;
            }
            //切换管道模式
            if (InnoVault.TileProcessors.TileProcessorLoader.AutoPositionGetTP(i, j, out ItemPipelineTP tp)) {
                tp.CycleMode();
                return true;
            }
            return false;
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<ItemPipeline>();
        }

        public override bool PreDraw(int i, int j, Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch) => false;
    }
}
