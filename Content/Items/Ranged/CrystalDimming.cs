using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CrystalDimming : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimming";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 122;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.1f };
            Item.SetCartridgeGun<CrystalDimmingHeld>(900);
            Item.value = Item.buyPrice(0, 16, 75, 0);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(CWRID.Item_PridefulHuntersPlanarRipper, 1).
                AddIngredient(CWRID.Item_RuinousSoul, 12).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
