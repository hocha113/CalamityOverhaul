using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class AvalancheM60 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "AvalancheM60";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.useAmmo = AmmoID.Snowball;
            Item.SetCartridgeGun<AvalancheM60HeldProj>(800);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<FlurrystormCannon>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient<CryonicBar>(3).
                AddIngredient<CoreofEleum>(5).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
