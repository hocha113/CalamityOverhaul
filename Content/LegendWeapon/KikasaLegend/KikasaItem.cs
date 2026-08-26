using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    internal class KikasaItem : ModItem
    {
        public override void SetDefaults() {
            Item.width = 50;
            Item.height = 54;
            //基伤与等级缩放由成长层 KikasaOverride 接管,这里只落个 L0 值
            Item.damage = 8;
            Item.DamageType = DamageClass.Summon;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.channel = true;
            Item.UseSound = null; //音效在悬伞弹幕里播,避免与物品使用声叠
            Item.shoot = ModContent.ProjectileType<KikasaRainUmbrella>();
            Item.shootSpeed = 1f;
            Item.value = Terraria.Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Purple;
            //持有即常驻:悬伞由 CWRItem.HoldItem 的持有生成机制维持,使用只是指挥
            Item.CWR().heldProjType = ModContent.ProjectileType<KikasaRainUmbrella>();
        }

        /// <summary>右键=倒撑蓄力重击</summary>
        public override bool AltFunctionUse(Player player) => true;

        /// <summary>返回 false 接管 tooltip 全绘制(行数据仍来自 ModifyTooltips 管线)</summary>
        public override bool PreDrawTooltip(ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
            => KikasaItemTooltipPanel.Draw(Item, lines, x, y);

        //常驻伞由持有生成,使用只负责指挥,不再以伞在场封锁。
        //鬼梦封禁不在这里:KikasaDreamPlayer.SetControls 按梦界圆全局压 noItems,
        //人人失能不走单件物品的 CanUseItem;唤犬读原始输入、各切换键不经物品使用,均不受封禁

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool alt = player.altFunctionUse == 2;
            //指挥常驻伞直入攻击态:左=墨雨,右=倒撑蓄墨
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is KikasaRainUmbrella umbrella) {
                    umbrella.CommandAttack(alt);
                    return false;
                }
            }
            //兜底:常驻伞尚未就位(刚切装同帧点击),生成后立即下达攻击指令
            int p = Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero,
                type, damage, knockback, player.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles
                && Main.projectile[p].ModProjectile is KikasaRainUmbrella fresh) {
                fresh.CommandAttack(alt);
            }
            return false;
        }
    }
}
