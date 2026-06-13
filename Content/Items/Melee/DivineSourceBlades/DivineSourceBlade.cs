using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃 —— 三段连击：两段迅捷连斩接一记蓄力大斩切，
    /// 大斩切轰出巨型新月剑气
    /// </summary>
    internal class DivineSourceBlade : ModItem
    {
        public override string Texture => DivineSourceBladeFX.BladeTexture;

        /// <summary>停手超过该时长后连击重置回第一段</summary>
        private const int ComboResetTicks = 120;
        private int combo;
        private uint lastShootTick;

        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 164;
            Item.damage = 1560;
            Item.DamageType = DamageClass.Melee;
            //实际节奏由手持弹幕存活期接管：快斩约 21 帧，大斩切约 37 帧
            Item.useAnimation = Item.useTime = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.value = Item.buyPrice(0, 33, 15, 0);
            Item.rare = ItemRarityID.Red;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DivineSourceBladeHeld>();
            Item.shootSpeed = 1f;
            Item.UseSound = null;
        }

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DivineSourceBladeHeld>()] > 0) {
                return false;
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Main.GameUpdateCount - lastShootTick > ComboResetTicks) {
                combo = 0;
            }
            lastShootTick = Main.GameUpdateCount;

            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback,
                player.whoAmI, ai0: combo);

            combo = (combo + 1) % 3;
            return false;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;

        public override void AddRecipes() {
            if (CWRID.Item_AuricBar > 0 && CWRID.Item_Terratomere > 0
                && CWRID.Item_Excelsus > 0 && CWRID.Tile_CosmicAnvil > 0) {
                CreateRecipe().
                AddIngredient(CWRID.Item_AuricBar, 5).
                AddIngredient(CWRID.Item_Terratomere).
                AddIngredient(CWRID.Item_Excelsus).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
            }
        }
    }
}
