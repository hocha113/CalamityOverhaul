using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class FlintAxe : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/FlintAxe";
        public override void SetDefaults() {
            Item.damage = 11;
            Item.knockBack = 1f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.axe = 8;
            Item.tileBoost -= 1;
            Item.DamageType = DamageClass.Melee;
            Item.width = 30;
            Item.height = 30;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Terraria.Item.buyPrice(0, 0, 0, 25);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit -= 1;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Flint>(4).
                AddIngredient(ItemID.VineRope, 3).
                AddIngredient(ItemID.Wood ,8).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
