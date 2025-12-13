using CalamityMod.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class SimpleMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "MuzzleBrake";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 1, 15, 0);
            Item.rare = ItemRarityID.Lime;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrakeLevel = 1;
            modplayer.PressureIncrease -= 0.4f;
            player.GetDamage<RangedDamageClass>() -= 0.15f;
            player.GetCritChance<RangedDamageClass>() += 5f;
            player.aggro -= 200;
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
                .AddIngredient(CWRID.Item_DubiousPlating, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
