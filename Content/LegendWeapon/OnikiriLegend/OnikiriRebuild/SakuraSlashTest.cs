using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraSlashTest : ModItem
{
    public override string Texture => "CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/OnikiriItem";

    public override void SetDefaults()
    {
        Item.damage = 50;
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<SakuraSlashProj>();
        Item.shootSpeed = 0f;
        Item.knockBack = 6f;
        Item.rare = ItemRarityID.Blue;
        Item.autoReuse = true;
    }

    public override bool CanUseItem(Player player) => SakuraActionGuard.CanStart(player);

    public override bool AltFunctionUse(Player player) => true;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    {
        //SakuraDashProj.Spawn(source, player.Center, (Main.MouseWorld - player.Center).SafeNormalize(new Vector2(player.direction, 0f)), player.whoAmI);
        Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<SakuraReadyStanceProj>(), 0, 0, player.whoAmI);
        return false;
        int projectileType = player.altFunctionUse == 2
            ? ModContent.ProjectileType<SakuraSlash2Proj>()
            : ModContent.ProjectileType<SakuraSlashProj>();
        Projectile.NewProjectile(source, player.Center, Vector2.Zero,
            projectileType, damage, knockback, player.whoAmI);
        return false;
    }

    public override void AddRecipes()
    {
        CreateRecipe().AddIngredient(ItemID.DirtBlock).Register();
    }
}
