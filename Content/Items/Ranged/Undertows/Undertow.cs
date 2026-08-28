using CalamityOverhaul.Content.NPCs.SeaShrimp;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Undertows
{
    /// <summary>
    /// 潮渊，深渊长弓。左键把任意箭矢化为渊棘箭，命中坍缩出空化泡；
    /// 右键蓄力三段，拉满松手射出渊压重箭，贯穿并沿途拖拽，终点引爆内爆。
    /// 拉弦与发射在 <see cref="UndertowHeld"/>
    /// </summary>
    internal class Undertow : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Undertow";

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 66;
            Item.damage = 74;
            Item.useAnimation = Item.useTime = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.knockBack = 3f;
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Lime;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.sellPrice(0, 12);
            Item.crit = 8;
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<UndertowHeld>();
        }

        //右键蓄力
        public override bool AltFunctionUse(Player player) => true;

        //放箭时由手持弹幕拾取弹药
        public override bool CanConsumeAmmo(Item ammo, Player player) => UndertowHeld.AmmoConsumeContext;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕接管左右键,全松键后自毁
            int heldType = ModContent.ProjectileType<UndertowHeld>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                CreateRecipe().
                    AddIngredient<SeaShrimpShell>(8).
                    AddIngredient(CWRID.Item_Lumenyl, 8).
                    AddIngredient(CWRID.Item_DepthCells, 12).
                    AddIngredient(CWRID.Item_Voidstone, 14).
                    AddIngredient(ItemID.ChlorophyteBar, 8).
                    AddTile(TileID.MythrilAnvil).
                    Register();
                return;
            }
            CreateRecipe().
                AddIngredient<SeaShrimpShell>(8).
                AddIngredient(ItemID.ChlorophyteBar, 12).
                AddIngredient(ItemID.SharkFin, 8).
                AddIngredient(ItemID.SoulofMight, 8).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
