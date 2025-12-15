using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AvalancheM60 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "AvalancheM60";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 62;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = 0.2f };
            Item.SetCartridgeGun<AvalancheM60Held>(400);
            Item.value = Item.buyPrice(0, 4, 75, 0);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<SnowQuayMK2>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient(CWRID.Item_CryonicBar, 3).
                AddIngredient(CWRID.Item_CoreofEleum, 5).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
