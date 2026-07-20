using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class NeutronStarIngot : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/NeutronStarIngot";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 17));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.createTile = ModContent.TileType<NeutronStarIngotTile>();
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_ShadowspecBar, CWRID.Item_AuricBar, CWRID.Item_CosmiliteBar
                , CWRID.Item_AshesofAnnihilation, CWRID.Item_ExoPrism, CWRID.Item_AscendantSpiritEssence
                , CWRID.Item_AerialiteBar, CWRID.Item_CryonicBar, CWRID.Item_PerennialBar, CWRID.Item_ScoriaBar
                , CWRID.Item_AstralBar, CWRID.Item_UelibloomBar)) {
                return;
            }
            //全时代锭材各一，取代原终焉合成阵列；13个暗影耀斑锭中12个折算自被移除的暗物质球
            CreateRecipe()
                .AddIngredient(ItemID.PlatinumBar)
                .AddIngredient(ItemID.MeteoriteBar)
                .AddIngredient(ItemID.HellstoneBar)
                .AddIngredient(ItemID.ChlorophyteBar)
                .AddIngredient(ItemID.ShroomiteBar)
                .AddIngredient(ItemID.SpectreBar)
                .AddIngredient(ItemID.LunarBar)
                .AddIngredient(ItemID.FragmentSolar)
                .AddIngredient(ItemID.FragmentVortex)
                .AddIngredient(ItemID.FragmentNebula)
                .AddIngredient(ItemID.FragmentStardust)
                .AddIngredient(CWRID.Item_AerialiteBar)
                .AddIngredient(CWRID.Item_CryonicBar)
                .AddIngredient(CWRID.Item_PerennialBar)
                .AddIngredient(CWRID.Item_ScoriaBar)
                .AddIngredient(CWRID.Item_AstralBar)
                .AddIngredient(CWRID.Item_UelibloomBar)
                .AddIngredient(CWRID.Item_AscendantSpiritEssence)
                .AddIngredient(CWRID.Item_ExoPrism)
                .AddIngredient(CWRID.Item_AshesofAnnihilation)
                .AddIngredient(CWRID.Item_CosmiliteBar)
                .AddIngredient(CWRID.Item_AuricBar)
                .AddIngredient(CWRID.Item_ShadowspecBar, 13)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }
    }
}
