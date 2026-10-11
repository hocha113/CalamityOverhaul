using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>只验证冲刺与斩击视觉，不接入正式鬼切，也不添加伤害或无敌帧。</summary>
public sealed class SakuraDashTest : ModItem
{
    public override string Texture => SakuraDashSystem.BasePath + "OnikiriItem";

    public override void SetDefaults()
    {
        Item.width = Item.height = 40;
        Item.useTime = Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noMelee = Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<SakuraDashProj>();
        Item.shootSpeed = 18f;
        Item.rare = ItemRarityID.Blue;
    }

    public override bool CanUseItem(Player player) => SakuraActionGuard.CanStart(player);

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    {
        if (player.whoAmI == Main.myPlayer)
            SakuraDashProj.Spawn(source, player.Center, velocity, player.whoAmI);
        return false;
    }

    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.DirtBlock).Register();
}
