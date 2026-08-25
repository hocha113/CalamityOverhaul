using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShimmerTransmuters
{
    /// <summary>微光转化槽:自动化原版微光转化的困难模式机器(占位贴图沿用热能电池,待专属美术)</summary>
    internal class ShimmerTransmuter : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBattery";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 3, 0, 0);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<ShimmerTransmuterTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.HallowedBar, 12).
                AddIngredient(ItemID.SoulofLight, 8).
                AddIngredient(ItemID.CrystalShard, 6).
                AddIngredient(CWRID.Item_DubiousPlating, 12).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 12).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.HallowedBar, 12).
                AddIngredient(ItemID.SoulofLight, 8).
                AddIngredient(ItemID.CrystalShard, 6).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }
}
