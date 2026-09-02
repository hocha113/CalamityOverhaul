using CalamityOverhaul.Content.Rarities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class PrecisionMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "MuzzleBrakeII";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 6, 15, 0);
            Item.rare = ModContent.RarityType<LapisRarity>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetDamage<RangedDamageClass>() += 0.08f;
            player.GetCritChance<RangedDamageClass>() += 10f;
            player.GetAttackSpeed<RangedDamageClass>() += 0.08f;
            player.aggro -= 400;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<EyeOfSingularity>()
                && incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            if (CWRID.Item_PlasmaDriveCore > 0) {
                _ = CreateRecipe()
                .AddIngredient<SimpleMuzzleBrake>()
                .AddIngredient(CWRID.Item_PlasmaDriveCore)
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
            }
            else {
                CreateRecipe()
                .AddIngredient<SimpleMuzzleBrake>()
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
            }
        }
    }
}
