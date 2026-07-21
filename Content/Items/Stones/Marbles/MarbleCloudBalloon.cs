using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>瓶中大理石二段跳 + 大理石气球砸地</summary>
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
            MarbleBalloonPlayer modPlayer = player.GetModPlayer<MarbleBalloonPlayer>();
            modPlayer.Equipped = true;
            //二段跳粒子双份（云雾+石屑）
            modPlayer.CloudJumpVariant = true;
            player.noFallDmg = true;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
            => MarbleBalloon.CanEquipWithBalloon(equippedItem, incomingItem);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<MarbleinaBottle>()
                .AddIngredient<MarbleBalloon>()
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
