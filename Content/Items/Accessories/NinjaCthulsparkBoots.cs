using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>忍法克苏鲁闪耀靴:克苏鲁闪耀靴加上忍者大师装备,巨眼系上了头巾</summary>
    internal class NinjaCthulsparkBoots : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "NinjaCthulsparkBoots";

        //忍者装备放在靴与盾之前:闪避与爬墙照常生效,冲刺归属在下方统一定型
        internal static readonly int[] FuseSources = [
            ItemID.AmphibianBoots, ItemID.MasterNinjaGear, ItemID.TerrasparkBoots,
            ItemID.HorseshoeBundle, ItemID.EoCShield];

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 38;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 10, 0, 0);
            Item.rare = ItemRarityID.Yellow;
            //穿戴外观与克苏鲁闪耀靴一致,忍者装备无可见部件
            Item.shoeSlot = new Item(ItemID.TerrasparkBoots).shoeSlot;
            Item.balloonSlot = new Item(ItemID.HorseshoeBundle).balloonSlot;
            Item.shieldSlot = new Item(ItemID.EoCShield).shieldSlot;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            FrogsparkBoots.ApplyFuse(player, hideVisual, FuseSources);
            //足袋冲刺与盾冲刺同源冲突,固定用克苏鲁之盾的撞击冲刺(2=盾撞)
            player.dashType = 2;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CthulsparkBoots>()
                .AddIngredient(ItemID.MasterNinjaGear)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
