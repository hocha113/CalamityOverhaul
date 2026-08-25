using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃，四拍连击(逆时针/顺时针/椭圆环斩/椭圆反斩)。
    /// 命中缓慢充能，右键消耗整条充能进入 7 秒强化
    /// </summary>
    internal class DivineSourceBlade : ModItem
    {
        public override string Texture => DivineSourceBladeFX.BladeTexture;

        /// <summary>停手超过该时长后连击重置回第一拍</summary>
        private const int ComboResetTicks = 120;

        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 164;
            Item.damage = 1560;
            Item.DamageType = DamageClass.Melee;
            //节奏由手持存活期接管(快拍~21/椭圆~32/终结~37 帧，吃近战攻速)
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

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                //右键只在充能满且未激活时可用
                DivineSourcePlayer mp = player.GetModPlayer<DivineSourcePlayer>();
                return !mp.Empowered && mp.Charge >= 1f;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<DivineSourceBladeHeld>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            DivineSourcePlayer mp = player.GetModPlayer<DivineSourcePlayer>();

            if (player.altFunctionUse == 2) {
                mp.TryConsumeFullCharge();
                return false;
            }

            if (Main.GameUpdateCount - mp.LastSwingTick > ComboResetTicks) {
                mp.ComboStage = 0;
            }
            mp.LastSwingTick = Main.GameUpdateCount;

            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback,
                player.whoAmI, ai0: mp.ComboStage, ai1: mp.Empowered ? 1f : 0f);

            mp.ComboStage = (mp.ComboStage + 1) % 4;
            return false;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (player.HasBuff<DivineSourceChargeBuff>()) {
                damage *= DivineSourcePlayer.EmpowerDamageMul;
            }
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
