using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石云朵气球：整合瓶中大理石的二段跳与大理石气球的砸地能力</summary>
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
            //合成件身份：二段跳粒子升级为"白云雾+金石屑"双份形态
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
