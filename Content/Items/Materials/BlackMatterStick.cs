using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class BlackMatterStick : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/BlackMatterStick";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 18));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems5;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return base.PreDrawInWorld(spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar)
                .AddIngredient(ItemID.SpectreBar)
                .AddIngredient(ItemID.ChlorophyteBar)
                .AddIngredient(ItemID.HellstoneBar)
                .AddIngredient(ItemID.ShroomiteBar)
                .AddIngredient<ShadowspecBar>()
                .AddIngredient<AuricBar>()
                .AddIngredient<AerialiteBar>()
                .AddIngredient<AstralBar>()
                .AddIngredient<CosmiliteBar>()
                .AddIngredient<CryonicBar>()
                .AddIngredient<PerennialBar>()
                .AddIngredient<UelibloomBar>()
                .AddIngredient<ScoriaBar>()
                .AddIngredient<PestilenceIngot>()
                .AddIngredient<MiracleMatter>()
                .AddIngredient<ExoPrism>()
                .AddIngredient<AshesofAnnihilation>()
                .AddIngredient<AscendantSpiritEssence>()
                .AddIngredient<SpectralMatter>()
                .AddIngredient<DarkMatterBall>(11)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
