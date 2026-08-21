using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 传奇武器·鬼伞。第一能力模块：血湖领域——持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Domain"/> 开阖，
    /// 输入与状态机在 <see cref="KikasaDomains.KikasaDomainPlayer"/>；
    /// 领域含鬼雨异化表里形态——按 <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/>
    /// （默认中键）血湖沸腾倒转切换血/雨形态。
    /// 第二能力模块：湖藏——领域中持物按 <see cref="Common.CWRKeySystem.Kikasa_Sink"/>
    /// 沉物入湖存储，持伞按 <see cref="Common.CWRKeySystem.Legend_UIControl"/> 开湖窗提取；
    /// 数据与输入在 <see cref="KikasaVaults.KikasaVaultPlayer"/>，
    /// 沉浮演出在 <see cref="KikasaVaults.KikasaLakeFX"/>。
    /// 第三能力模块：沉影编成——沉溺过的 boss 永久入册，画境点血湖铺开沉影盘，
    /// 三席影位驻影即役使；记录在 <see cref="KikasaServants.KikasaServantPlayer"/>，
    /// 穷举条目在 <see cref="KikasaServants.KikasaServantIndex"/>，
    /// 焰/魇/潦门控与组合边在 <see cref="KikasaServants.KikasaEffigyBoard"/>。
    /// 第四能力模块：普攻·墨雨——按住左键撑出悬伞
    /// <see cref="KikasaRains.KikasaRainUmbrella"/>，头顶自旋按节拍降下大墨滴追踪敌人。
    /// 第五能力模块：鬼梦——魇影驻湖倒影自醒；长按
    /// <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/> 拉入鬼梦
    /// （湖沸腾倒转，红天村落、湖水不见；物品封禁、左键连唤恶犬，重按归返；
    /// 梦中人人失能——梦界内远程射弹无法存在，本伞左右键亦不可用）；
    /// 相位与包络在 <see cref="KikasaDomains.KikasaDomainPlayer"/> 与
    /// <see cref="KikasaDreams.KikasaDreamDirector"/>，玩家锁与唤犬在
    /// <see cref="KikasaDreams.KikasaDreamPlayer"/>，禁弹辨别在
    /// <see cref="KikasaDreams.KikasaDreamProjectileBan"/>。
    /// 第六能力模块：大范围重启——鬼雨形态下持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Restart"/>（与其余传奇重启共键），屏幕定格成黑白照片、
    /// 被雨痕冲刷揭开，场内 NPC 与玩家沿位置历史倒退回数秒前，雨滴倒飞，
    /// 结算时范围内玩家回满、清 debuff，全程无敌；
    /// 权威与时间轴在 <see cref="KikasaResets.KikasaReset"/>，
    /// 输入在 <see cref="KikasaResets.KikasaResetPlayer"/>。
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
        }

        /// <summary>右键=倒撑蓄力重击</summary>
        public override bool AltFunctionUse(Player player) => true;

        //悬伞在场时不重复开伞；鬼梦世界里左右键皆封——梦中失能对梦主也不例外，
        //唤犬读原始输入、各切换键不经物品使用，均不受此限
        public override bool CanUseItem(Player player)
            => !KikasaDream.DreamWorldAt(player.Center)
            && player.ownedProjectileCounts[ModContent.ProjectileType<KikasaRainUmbrella>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //ai[0]:0=墨雨,1=蓄力倒撑(重击模块接管)
            float mode = player.altFunctionUse == 2 ? 1f : 0f;
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero,
                type, damage, knockback, player.whoAmI, mode);
            return false;
        }
    }
}
