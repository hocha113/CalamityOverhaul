using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class FlintPick : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/FlintPick";
        public override void SetDefaults() {
            Item.damage = 10;
            Item.knockBack = 1f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.pick = 20;
            Item.DamageType = DamageClass.Melee;
            Item.width = 46;
            Item.height = 46;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Terraria.Item.buyPrice(0, 0, 0, 25);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit -= 1;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Flint>(6).
                AddIngredient(ItemID.VineRope, 3).
                AddIngredient(ItemID.Wood, 8).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
