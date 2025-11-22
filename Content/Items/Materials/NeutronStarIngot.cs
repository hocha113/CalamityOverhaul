using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronStarIngot;
        }
    }
}
