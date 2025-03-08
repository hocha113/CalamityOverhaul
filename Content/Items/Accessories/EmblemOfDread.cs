using CalamityMod.Items.Accessories;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>
    /// 惧亡者之证
    /// </summary>
    internal class EmblemOfDread : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "EmblemOfDread";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 3));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_EmblemOfDread;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<ElementalGauntlet>()
                .AddIngredient<Affliction>()
                .AddIngredient<DarkSunRing>()
                .AddIngredient<DraedonsHeart>()
                .AddIngredient<AsgardianAegis>()
                .AddIngredient<Radiance>()
                .AddIngredient<YharimsGift>()
                .AddIngredient<TheSponge>()
                .AddIngredient<TheAmalgam>()
                .AddIngredient<WarbanneroftheSun>()
                .AddIngredient<ReaperToothNecklace>()
                .AddIngredient<OccultSkullCrown>()
                .AddIngredient<ChaliceOfTheBloodGod>()
                .AddIngredient<NeutronStarIngot>(12)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
