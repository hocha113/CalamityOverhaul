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
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 102;
            Item.useAmmo = AmmoID.Snowball;
            Item.value = Terraria.Item.buyPrice(0, 35, 5, 5);
            Item.UseSound = SoundID.Item36 with { Pitch = 0.2f };
            Item.SetCartridgeGun<DarkFrostSolsticeHeld>(1200);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(CWRID.Item_Kingsbane).
                AddIngredient(CWRID.Item_ShadowspecBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 10).
                AddTile(CWRID.Tile_DraedonsForge).
                Register();
        }
    }
}
