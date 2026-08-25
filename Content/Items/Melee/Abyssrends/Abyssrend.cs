using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 裂渊，深渊长兵器。左键交替尖端戳刺与侧面斩击，每一击撕出追踪暗流；
    /// 右键钳口咬住目标持续挤压，收束后引爆高压空化。连击与钳击冷却存在 <see cref="AbyssrendPlayer"/>
    /// </summary>
    internal class Abyssrend : ModItem
    {
        public override string Texture => AbyssrendFX.ItemTexture;

        private const int ComboResetTicks = 90;

        public override void SetDefaults() {
            Item.width = 64;
            Item.height = 66;
            Item.damage = 80;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.2f;
            Item.value = Item.sellPrice(0, 10);
            Item.rare = ItemRarityID.Lime;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<AbyssrendHeld>();
            Item.shootSpeed = 1f;
            Item.UseSound = null;
            Item.crit = 6;
        }

        public override bool MeleePrefix() => true;

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<AbyssrendHeld>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<AbyssrendClampHeld>()] > 0) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                return player.GetModPlayer<AbyssrendPlayer>().ClampReady;
            }
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            AbyssrendPlayer ap = player.GetModPlayer<AbyssrendPlayer>();
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            if (player.altFunctionUse == 2) {
                int target = AbyssrendClampHeld.FindTarget(player);
                Projectile.NewProjectile(source, player.Center, dir
                    , ModContent.ProjectileType<AbyssrendClampHeld>()
                    , damage, knockback, player.whoAmI, ai0: 0f, ai1: target);
                ap.ComboStage = 0;
                return false;
            }

            if (Main.GameUpdateCount - ap.LastSwingTick > ComboResetTicks) {
                ap.ComboStage = 0;
            }
            ap.LastSwingTick = Main.GameUpdateCount;

            int beat = ap.ComboStage % 4;
            float swingSign = beat == 2 ? -1f : 1f;
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback
                , player.whoAmI, ai0: beat, ai1: swingSign);

            ap.ComboStage = (ap.ComboStage + 1) % 4;
            return false;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 4;

        public override void AddRecipes() {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                CreateRecipe().
                    AddIngredient(CWRID.Item_Lumenyl, 8).
                    AddIngredient(CWRID.Item_DepthCells, 12).
                    AddIngredient(CWRID.Item_Voidstone, 16).
                    AddIngredient(ItemID.ChlorophyteBar, 8).
                    AddTile(TileID.MythrilAnvil).
                    Register();
                return;
            }
            CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 12).
                AddIngredient(ItemID.SharkFin, 8).
                AddIngredient(ItemID.SoulofMight, 8).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }

    /// <summary>裂渊每玩家状态：连击拍号与钳击冷却。不入库</summary>
    internal class AbyssrendPlayer : ModPlayer
    {
        public int ComboStage;
        public uint LastSwingTick;
        public uint ClampReadyAt;

        public bool ClampReady => Main.GameUpdateCount >= ClampReadyAt;

        public void SetClampCooldown(int ticks)
            => ClampReadyAt = Main.GameUpdateCount + (uint)Math.Max(0, ticks);
    }
}
