using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石云气球：整合瓶中大理石的二段跳与大理石气球的砸地能力</summary>
    internal class MarbleCloudBalloon : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 1, 10, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetJumpState<MarbleinaBottleJump>().Enable();
            player.GetModPlayer<MarbleBalloonPlayer>().Equipped = true;
            player.noFallDmg = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<MarbleinaBottle>()
                .AddIngredient<MarbleBalloon>()
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
