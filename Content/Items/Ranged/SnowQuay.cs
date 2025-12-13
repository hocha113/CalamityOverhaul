using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SnowQuay : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuay";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 22;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.2f };
            Item.SetCartridgeGun<SnowQuayHeld>(200);
            Item.value = Terraria.Item.buyPrice(0, 1, 75, 0);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient(CWRID.Item_FlurrystormCannon).
                AddIngredient(CWRID.Item_EssenceofEleum, 10).
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
        }
    }
}
