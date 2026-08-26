using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 传奇武器·鬼伞。第一能力模块：血湖领域，持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Domain"/> 开阖，
    /// 输入与状态机在 <see cref="KikasaDomains.KikasaDomainPlayer"/>；
    /// 领域含鬼雨异化表里形态，按 <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/>
    /// （默认中键）血湖沸腾倒转切换血/雨形态。
    /// 第二能力模块：湖藏，领域中持物按 <see cref="Common.CWRKeySystem.Kikasa_Sink"/>
    /// 沉物入湖存储，持伞按 <see cref="Common.CWRKeySystem.Legend_UIControl"/>
    /// 开「湖心景」全屏（<see cref="UI.Panorama.KikasaPanoramaUI"/>）在湖藏区点击提取；
    /// 数据与输入在 <see cref="KikasaVaults.KikasaVaultPlayer"/>，
    /// 沉浮演出在 <see cref="KikasaVaults.KikasaLakeFX"/>。
    /// 第三能力模块：沉影编成，沉溺过的 boss 永久入册，湖心景水线三席拾影点放编成，
    /// 快捷转盘逐席召/收（<see cref="UI.ServantWheel.KikasaServantWheelController"/>）；
    /// 记录在 <see cref="KikasaServants.KikasaServantPlayer"/>，
    /// 穷举条目在 <see cref="KikasaServants.KikasaServantIndex"/>，
    /// 焰/魇/潦增益与组合边在 <see cref="KikasaServants.KikasaEffigyBoard"/>。
    /// 第四能力模块：普攻·墨雨。鬼伞持有即常驻，平时悬在玩家背肩上方随行，
    /// 周围有敌且玩家未主动攻击时自行倾身抛洒墨滴自卫；按住左键悬伞
    /// <see cref="KikasaRains.KikasaRainUmbrella"/> 飞到头顶自旋，
    /// 按节拍降下追踪敌人的大墨滴，各动作态实时直入无前后摇。
    /// 第五能力模块：鬼梦，满水稳态倒影自醒；长按
    /// <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/> 拉入鬼梦
    /// （湖沸腾倒转，红天村落、湖水不见；物品封禁、左键连唤恶犬，重按归返；
    /// 梦中人人失能，梦界内远程射弹无法存在，本伞左右键亦不可用）；
    /// 相位与包络在 <see cref="KikasaDomains.KikasaDomainPlayer"/> 与
    /// <see cref="KikasaDreams.KikasaDreamDirector"/>，玩家锁与唤犬在
    /// <see cref="KikasaDreams.KikasaDreamPlayer"/>，禁弹辨别在
    /// <see cref="KikasaDreams.KikasaDreamProjectileBan"/>。
    /// 第六能力模块：大范围重启，鬼雨形态下持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Restart"/>（与其余传奇重启共键），屏幕定格成黑白照片、
    /// 被雨痕冲刷揭开，场内 NPC 与玩家沿位置历史倒退回数秒前，雨滴倒飞，
    /// 结算时范围内玩家回满、清 debuff，全程无敌；
    /// 权威与时间轴在 <see cref="KikasaResets.KikasaReset"/>，
    /// 输入在 <see cref="KikasaResets.KikasaResetPlayer"/>。
    /// 第七能力模块：鬼域传送，持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Teleport"/>（与其余传奇传送共键）
    /// 以水为媒介瞬移到指针处，瞬发无前后摇：双潭当帧砸开，数帧内人已渡到彼岸，
    /// 此岸水柱吞人、彼岸水柱喷发，常驻悬伞亲自扎水→隐没→破水弹回，
    /// 全程只有一把伞；不需要领域，血湖稳态里节奏与冷却更短、真湖随之荡波；
    /// 门面与输入在 <see cref="KikasaTeleports.KikasaTeleport"/> 与
    /// <see cref="KikasaTeleports.KikasaTeleportPlayer"/>，
    /// 水舞台在 <see cref="KikasaTeleports.KikasaTeleportProj"/>。
    /// 召唤师武器：召唤栏位化作栖在伞骨下的鬼，左右键普攻的节拍、滴数、
    /// 伤害与三档质变全按栏位数走，口径集中在 <see cref="KikasaOverride"/>；
    /// 传奇成长（伤害等级表+沉宴试炼路线）同在 <see cref="KikasaOverride"/> 与
    /// <see cref="KikasaData"/>，试炼注册在 <see cref="TrialQuests.KikasaTrialQuestLine"/>
    /// </summary>
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
