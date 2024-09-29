using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DecaySubstance : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DecaySubstance";
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 13);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems12;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, lightColor, Main.GameUpdateCount * 0.1f, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentVortex, 1)
                .AddIngredient(ItemID.FragmentNebula, 1)
                .AddIngredient(ItemID.FragmentSolar, 1)
                .AddIngredient(ItemID.FragmentStardust, 1)
                .AddIngredient(ItemID.LunarBar, 4)
                .AddIngredient<DecayParticles>(1)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
