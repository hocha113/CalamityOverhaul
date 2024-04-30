using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class SnowQuay : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuay";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.damage = 22;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.2f };
            Item.SetCartridgeGun<SnowQuayHeldProj>(400);
            Item.value = Terraria.Item.buyPrice(0, 1, 75, 0);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<FlurrystormCannon>().
                AddIngredient<EssenceofEleum>(10).
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
        }
    }
}
