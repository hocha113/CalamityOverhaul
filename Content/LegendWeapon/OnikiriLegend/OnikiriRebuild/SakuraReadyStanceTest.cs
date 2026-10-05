using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraReadyStanceTest : ModItem
{
    public override string Texture => SakuraFinaleAssets.TexturePath + "OnikiriItem";
    public override void SetDefaults()
    {
        Item.width = Item.height = 40;
        Item.useTime = Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noMelee = Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<SakuraReadyStanceProj>();
        Item.rare = ItemRarityID.Blue;
    }
    public override bool CanUseItem(Player player) => SakuraActionGuard.CanStart(player);
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    {
        if (player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, type, 0, 0f, player.whoAmI);
        return false;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.DirtBlock).Register();
}
