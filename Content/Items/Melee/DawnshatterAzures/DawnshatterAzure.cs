using CalamityOverhaul.Content.Rarities;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 苍穹破晓,五拍连段长枪,右键举枪突进,高空左键起手转下砸;连段状态住在 DawnshatterHeld,物品只做路由<br/>
    /// 可下砸状态每帧检测并画在物品栏图标上,玩家不用试错就知道这一下会砸
    /// </summary>
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        /// 下砸窗口:脚下首个立足面落在 [14,120) 格,太近地不值得砸,深渊上砸不到底
        private const int MinDropTiles = 14;
        private const int MaxDropTiles = 120;
        /// 本帧可下砸,UpdateInventory 维护
        private bool canDive;

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = Item.width = 54;
            Item.damage = 11200;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(6, 23, 75, 0);
            Item.rare = ModContent.RarityType<StarsilverRarity>();
            Item.shoot = ModContent.ProjectileType<DawnshatterHeld>();
            Item.shootSpeed = 1f;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_ShadowspecBar, CWRID.Item_RedSun, CWRID.Item_DraconicDestruction
                , CWRID.Item_DragonPow, CWRID.Item_DragonRage, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(ItemID.DayBreak)
                .AddIngredient(ItemID.FragmentSolar, 16)
                .AddIngredient(CWRID.Item_ShadowspecBar, 3)
                .AddIngredient(CWRID.Item_RedSun)
                .AddIngredient(CWRID.Item_DraconicDestruction)
                .AddIngredient(CWRID.Item_DragonPow)
                .AddIngredient(CWRID.Item_DragonRage)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterHeld>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterDash>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterDive>()] == 0;
        }

        public override void UpdateInventory(Player player) => canDive = CheckCanDive(player);

        /// <summary>脚下首个立足面(实心或平台)距离落在下砸窗口内;骑乘不砸</summary>
        private static bool CheckCanDive(Player player) {
            if (player.mount.Active) {
                return false;
            }
            Point tile = player.Center.ToTileCoordinates();
            int step = player.gravDir >= 0f ? 1 : -1;
            for (int i = 0; i < MaxDropTiles; i++) {
                int y = tile.Y + i * step;
                if (!WorldGen.InWorld(tile.X, y, 4)) {
                    return false;
                }
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasUnactuatedTile
                    && (Main.tileSolid[probe.TileType] || Main.tileSolidTop[probe.TileType])) {
                    return i >= MinDropTiles;
                }
            }
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DawnshatterDash>()
                    , (int)(damage * 2.2f), knockback * 1.5f, player.whoAmI);
                return false;
            }
            //高空起手转下砸;连段进行中 CanUseItem 已拦住,不会砸断连段
            if (canDive && CheckCanDive(player)) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DawnshatterDive>()
                    , damage * 5, knockback * 1.5f, player.whoAmI);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        /// <summary>可下砸时物品栏图标右下角亮金色下箭头,呼吸闪读作"待发"而不是常驻贴纸</summary>
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (!canDive || Main.gamePaused) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 anchor = position + frame.Size() * scale - new Vector2(6f, 4f) * scale;
            float breath = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
            Color gold = new Color(255, 202, 92) with { A = 0 } * breath;

            Vector2 tipDown = anchor + new Vector2(0f, 5f) * scale;
            DrawBar(spriteBatch, pixel, anchor + new Vector2(0f, -7f) * scale, tipDown, 2.4f * scale, gold);
            DrawBar(spriteBatch, pixel, anchor + new Vector2(-4.5f, -0.5f) * scale, tipDown, 2.4f * scale, gold);
            DrawBar(spriteBatch, pixel, anchor + new Vector2(4.5f, -0.5f) * scale, tipDown, 2.4f * scale, gold);
        }

        private static void DrawBar(SpriteBatch spriteBatch, Texture2D pixel
            , Vector2 from, Vector2 to, float thickness, Color color) {
            Vector2 delta = to - from;
            spriteBatch.Draw(pixel, from, new Rectangle(0, 0, 1, 1), color, delta.ToRotation()
                , new Vector2(0f, 0.5f), new Vector2(delta.Length(), thickness), SpriteEffects.None, 0f);
        }
    }
}
