using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DragonsScaleGreatswordProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DragonsScaleGreatsword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DragonsScaleGreatsword";
        public override void SetDefaults() {
            Item.height = 54;
            Item.width = 54;
            Item.damage = 556;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 16;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.5f;
            Item.UseSound = SoundID.Item60;
            Item.channel = true;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 4, 75, 0);
            Item.rare = ModContent.RarityType<Violet>();
            Item.shoot = ModContent.ProjectileType<DragonsScaleGreatswordBeam>();
            Item.shootSpeed = 7f;
            Item.SetKnifeHeld<DragonsScaleGreatswordHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 3;

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<PerennialBar>(15).
                AddIngredient<UelibloomBar>(15).
                AddIngredient(ItemID.ChlorophyteBar, 15).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
