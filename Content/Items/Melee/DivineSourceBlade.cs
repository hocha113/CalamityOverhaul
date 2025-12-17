using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DivineSourceBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DivineSourceBlade";
        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 560;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 15;
            Item.scale = 1;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item60;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 33, 15, 0);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<DivineSourceBladeProjectile>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<DivineSourceBladeHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                .AddIngredient(ItemID.FragmentSolar, 30)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
                return;
            }
            CreateRecipe().
                AddIngredient(CWRID.Item_AuricBar, 5).
                AddIngredient(CWRID.Item_Terratomere).
                AddIngredient(CWRID.Item_Excelsus).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }
}
