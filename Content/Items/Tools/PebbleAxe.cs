using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class PebbleAxe : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/PebbleAxe";
        public override void SetDefaults() {
            Item.damage = 8;
            Item.knockBack = 1f;
            Item.useTime = 11;
            Item.useAnimation = 22;
            Item.axe = 5;
            Item.tileBoost -= 2;
            Item.DamageType = DamageClass.Melee;
            Item.width = 30;
            Item.height = 30;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Terraria.Item.buyPrice(0, 0, 0, 15);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit -= 1;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Pebble>(4).
                AddIngredient(ItemID.VineRope, 3).
                AddIngredient(ItemID.Wood, 8).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
