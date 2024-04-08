using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class CrystalDimming : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimming";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.damage = 122;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetCartridgeGun<CrystalDimmingHeldProj>(900);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<AvalancheM60>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient<CryonicBar>(3).
                AddIngredient<CoreofEleum>(5).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
