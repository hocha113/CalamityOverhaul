using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class DarkFrostSolstice : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolstice";
        public static int ID { get; private set; }
        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.SetItemCopySD<Onyxia>();
            Item.damage = 102;
            Item.useAmmo = AmmoID.Snowball;
            Item.value = Terraria.Item.buyPrice(0, 35, 5, 5);
            Item.UseSound = SoundID.Item36 with { Pitch = 0.2f };
            Item.SetCartridgeGun<DarkFrostSolsticeHeld>(1200);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient<Kingsbane>().
                AddIngredient<ShadowspecBar>(5).
                AddIngredient<EndothermicEnergy>(10).
                AddTile(ModContent.TileType<DraedonsForge>()).
                Register();
        }
    }
}
