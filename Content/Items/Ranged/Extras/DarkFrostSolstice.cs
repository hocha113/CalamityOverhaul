using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class DarkFrostSolstice : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolstice";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.damage = 102;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetCartridgeGun<DarkFrostSolsticeHeldProj>(1200);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient<CryonicBar>(3).
                AddIngredient<CoreofEleum>(5).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
