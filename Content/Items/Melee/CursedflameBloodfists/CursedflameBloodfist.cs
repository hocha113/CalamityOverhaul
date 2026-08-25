using CalamityOverhaul.Content.DamageModify;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.CursedflameBloodfists
{
    /// <summary>
    /// 咒焰血拳，近战与魔法双系。按住左键原地灌出高速连打，
    /// 每一拳既打身周范围，也顺着准星轰出成串的飞行火焰拳
    /// </summary>
    internal class CursedflameBloodfist : ModItem
    {
        public override string Texture => CursedflameFX.ItemTexture;

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 116;
            //单发很小，节奏堆出 DPS：约 8.5 拳/秒 + 每拳 2~3 只 0.4 倍飞拳
            Item.damage = 28;
            Item.DamageType = MeleeMagicDamageClass.Instance;
            //起手扣一次，连打期间的持续消耗由握持弹幕结算
            Item.mana = 8;
            //节奏由握持弹幕接管，面板攻速仍然生效
            Item.useAnimation = Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.1f;
            Item.value = Item.sellPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Pink;
            Item.autoReuse = true;
            Item.channel = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<CursedflameBloodfistHeld>();
            Item.shootSpeed = 1f;
            Item.UseSound = null;
        }

        public override bool MeleePrefix() => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CursedflameBloodfistHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddRecipeGroup(CWRCrafted.AdamantiteBarGroup, 12).
            AddIngredient(ItemID.TissueSample, 15).
            AddIngredient(ItemID.CursedFlame, 12).
            AddTile(TileID.MythrilAnvil).
            Register();
        }
    }
}
