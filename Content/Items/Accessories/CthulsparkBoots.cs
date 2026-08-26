using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>克苏鲁闪耀靴:蛙跃闪耀靴加上马蹄气球束与克苏鲁之盾,巨眼气球随行</summary>
    internal class CthulsparkBoots : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "CthulsparkBoots";

        //盾放最后,保证冲刺由克苏鲁之盾定型
        internal static readonly int[] FuseSources = [
            ItemID.AmphibianBoots, ItemID.TerrasparkBoots,
            ItemID.HorseshoeBundle, ItemID.EoCShield];

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 38;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 7, 0, 0);
            Item.rare = ItemRarityID.LightPurple;
            //穿戴外观:闪耀靴鞋部+马蹄气球束+克苏鲁之盾,交给原版可见饰品管线
            Item.shoeSlot = new Item(ItemID.TerrasparkBoots).shoeSlot;
            Item.balloonSlot = new Item(ItemID.HorseshoeBundle).balloonSlot;
            Item.shieldSlot = new Item(ItemID.EoCShield).shieldSlot;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => FrogsparkBoots.ApplyFuse(player, hideVisual, FuseSources);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<FrogsparkBoots>()
                .AddIngredient(ItemID.HorseshoeBundle)
                .AddIngredient(ItemID.EoCShield)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
