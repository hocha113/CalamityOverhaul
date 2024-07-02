using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
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
            Item.UseSound = SoundID.Item36 with { Pitch = -0.1f };
            Item.SetCartridgeGun<CrystalDimmingHeldProj>(900);
            Item.value = Terraria.Item.buyPrice(0, 16, 75, 0);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient<PridefulHuntersPlanarRipper>(1).
                AddIngredient<RuinousSoul>(12).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
