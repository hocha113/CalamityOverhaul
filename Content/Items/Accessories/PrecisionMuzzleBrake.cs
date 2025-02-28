using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class PrecisionMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "MuzzleBrakeII";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 6, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrakeLevel = 2;
            modplayer.PressureIncrease -= 0.55f;
            player.GetDamage<RangedDamageClass>() -= 0.1f;
            player.aggro -= 400;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<NeutronStarMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<SimpleMuzzleBrake>()
                .AddIngredient<PlasmaDriveCore>()
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
