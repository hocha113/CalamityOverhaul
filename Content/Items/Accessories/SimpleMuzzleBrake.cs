using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class SimpleMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item + "MuzzleBrake";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Terraria.Item.buyPrice(0, 1, 15, 0);
            Item.rare = ItemRarityID.Lime;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrake = true;
            modplayer.LoadMuzzleBrakeLevel = 1;
            modplayer.PressureIncrease -= 0.4f;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<NeutronStarMuzzleBrake>() 
                && incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>() 
                && incomingItem.type != ModContent.ItemType<PrecisionMuzzleBrake>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<EnergyCore>(2)
                .AddIngredient<WulfrumMetalScrap>(5)
                .AddIngredient<DubiousPlating>(5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
