using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>蛙跃闪耀靴:泰拉闪耀靴与两栖靴的融合,继承两条子合成树的全部效果</summary>
    internal class FrogsparkBoots : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "FrogsparkBoots";

        //融合来源,按顺序生效:跑速取决于最后生效的靴子,故泰拉闪耀靴放最后
        internal static readonly int[] FuseSources = [ItemID.AmphibianBoots, ItemID.TerrasparkBoots];

        /// <summary>逐件套用原版饰品的功能效果,供三个等级的融合靴共用,外观走物品自身的视觉槽位</summary>
        internal static void ApplyFuse(Player player, bool hideVisual, int[] sources) {
            foreach (int type in sources) {
                player.ApplyEquipFunctional(ContentSamples.ItemsByType[type], hideVisual);
            }
        }

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 28;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Pink;
            //穿戴外观沿用泰拉闪耀靴,交给原版可见饰品管线处理显隐与染料
            Item.shoeSlot = new Item(ItemID.TerrasparkBoots).shoeSlot;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => ApplyFuse(player, hideVisual, FuseSources);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.TerrasparkBoots)
                .AddIngredient(ItemID.AmphibianBoots)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
