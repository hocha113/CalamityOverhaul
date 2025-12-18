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
            Item.rare = CWRID.Rarity_Turquoise;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrakeLevel = 2;
            modplayer.PressureIncrease -= 0.55f;
            player.GetDamage<RangedDamageClass>() -= 0.1f;
            player.GetCritChance<RangedDamageClass>() += 10f;
            player.aggro -= 400;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<NeutronStarMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                .AddIngredient<SimpleMuzzleBrake>()
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
                return;
            }
            _ = CreateRecipe()
                .AddIngredient<SimpleMuzzleBrake>()
                .AddIngredient(CWRID.Item_PlasmaDriveCore)
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
