using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class GhostFireWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "GhostFireWhip";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<GhostFireWhipProjectile>(), 220, 1, 12, 30);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 16, 5, 75);
            Item.rare = ItemRarityID.Cyan;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ItemID.BoneWhip)
                .AddIngredient(ModContent.ItemType<RuinousSoul>(), 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
