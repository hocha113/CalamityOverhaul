using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class ElementMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "MuzzleBrakeIII";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 22, 15, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetDamage<RangedDamageClass>() += 0.12f;
            player.GetCritChance<RangedDamageClass>() += 15f;
            player.GetAttackSpeed<RangedDamageClass>() += 0.12f;
            player.aggro -= 600;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<EyeOfSingularity>()
                && incomingItem.type != ModContent.ItemType<PrecisionMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            if (CWRID.Item_LifeAlloy > 0) {
                _ = CreateRecipe()
                .AddIngredient<PrecisionMuzzleBrake>()
                .AddIngredient(CWRID.Item_LifeAlloy, 5)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
            else {
                CreateRecipe()
                .AddIngredient<PrecisionMuzzleBrake>()
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
        }
    }
}
