using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦禁弹："梦中人人失能"的射弹面，梦界内不容飞行射弹存在，
    /// 敌人的远程弹一出现就被梦吞没，只剩恶犬与獠牙说话。
    /// 一致性模型与湖面物理同款：服务器不持有领域相位（KikasaDomainNet 既定契约），
    /// 而命中本就在判定端本地结算（敌弹=受害者本机、友弹=持有者本机），
    /// 每端从已同步的快照跑同一条确定性规则各自熄灭，无需任何包。
    /// 辨别机制在 <see cref="IsSwallowed"/>：只吞"明显是远程攻击"的自由飞行弹，
    /// 功能性/手持类/随从本体从宽豁免，漏放一发冷枪比误杀钩爪代价小
    /// </summary>
    internal class KikasaDreamProjectileBan : ModSystem
    {
        /// <summary>本帧梦界圆心快照，通常为空；空表快速短路整套扫描</summary>
        private static readonly List<Vector2> dreamCenters = [];

        /// <summary>吞没墨雾的帧内限量，拉入结算帧一口气吞掉整屏弹幕时别刷屏</summary>
        private static int fxBudget;

        private const int MaxFxPerFrame = 10;

        /// <summary>此刻是否存在任何梦世界，出生截杀的快门</summary>
        internal static bool AnyDreamWorld => dreamCenters.Count > 0;

        /// <summary>
        /// 扫在一切实体更新之前：敌弹对玩家的判伤发生在 Player.Update 里，
        /// 网络新到的敌弹必须在玩家扫描它之前被吞掉，才没有落伤窗口
        /// </summary>
        public override void PreUpdateEntities() {
            fxBudget = MaxFxPerFrame;
            KikasaDream.CollectDreamWorldCenters(dreamCenters);
            if (dreamCenters.Count == 0) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && InAnyDream(proj.Center) && IsSwallowed(proj)) {
                    Swallow(proj);
                }
            }
        }

        public override void ClearWorld() => dreamCenters.Clear();

        /// <summary>该弹此刻是否该被吞：位置在梦界内且属被吞类别。出生截杀共用</summary>
        internal static bool SwallowedAt(Projectile proj)
            => InAnyDream(proj.Center) && IsSwallowed(proj);

        private static bool InAnyDream(Vector2 pos) {
            for (int i = 0; i < dreamCenters.Count; i++) {
                if (Vector2.DistanceSquared(pos, dreamCenters[i])
                    <= KikasaDream.WorldRange * KikasaDream.WorldRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 辨别机制：该射弹是否属于梦要吞的"远程攻击弹"。
        /// 白名单从宽，一切功能弹、手持弹、随从本体都放行，恶犬（minion+projPet）天然豁免
        /// </summary>
        internal static bool IsSwallowed(Projectile proj) {
            //非攻击性=功能弹：墓碑/传送门/星云拾取/telegraph 等零伤实体一概不碰
            if (proj.damage <= 0 || (!proj.hostile && !proj.friendly)) {
                return false;
            }
            //随从/哨兵/宠物是持续在场的"存在"而非射弹
            if (proj.minion || proj.sentry || proj.minionSlots > 0f
                || Main.projPet[proj.type]) {
                return false;
            }
            //钩爪与浮标：移动与钓鱼的功能命脉
            if (Main.projHook[proj.type] || proj.bobber) {
                return false;
            }
            //机关射弹属于世界而非"某人的远程能力"
            if (proj.trap) {
                return false;
            }
            //鞭与悠悠球：注册集优先，兼容不走原版 aiStyle 的模组货（悠悠球以寿命集为准，默认 -1）
            if (ProjectileID.Sets.IsAWhip[proj.type]
                || ProjectileID.Sets.YoyosLifeTimeMultiplier[proj.type] != -1f) {
                return false;
            }
            //手上连着的东西不吞：钩兜底/链锚/链锤/矛/钻锯/原版持握/悠悠球/鞭
            if (proj.aiStyle is ProjAIStyleID.Hook or ProjAIStyleID.Harpoon
                or ProjAIStyleID.Flail or ProjAIStyleID.Spear or ProjAIStyleID.Drill
                or ProjAIStyleID.HeldProjectile or ProjAIStyleID.Yoyo or ProjAIStyleID.Whip) {
                return false;
            }
            //隐藏层多为内部挂件；需持有者视线的是手持近战，吞了会拆别人的状态机
            if (proj.hide || proj.ownerHitCheck) {
                return false;
            }
            //InnoVault 手持弹幕：本模组全部枪械/挥砍的手持层
            if (proj.ModProjectile is BaseHeldProj) {
                return false;
            }
            //真实玩家手里正举着的原版手持弹兜底
            if (proj.owner >= 0 && proj.owner < Main.maxPlayers) {
                Player owner = Main.player[proj.owner];
                if (owner?.active == true && owner.heldProj == proj.whoAmI) {
                    return false;
                }
            }
            //时停豁免名单=各系统标记过的"系统级、别碰"弹幕（传奇技能弹等）
            if (CWRLoad.ProjValue.ImmuneFrozen.TryGetValue(proj.type, out bool immune) && immune) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 就地熄灭。不走 Kill：爆炸类的死亡结算会让"被梦吞没"炸出真实伤害；
        /// 也不发包：各端同规则自行熄灭即一致，服务器那份（若有）无判伤权、自然到寿
        /// </summary>
        internal static void Swallow(Projectile proj) {
            SwallowFx(proj);
            proj.active = false;
        }

        //吞没确认拍：射弹沉进梦里的位置翻起一小口墨雾，只在看得见这场梦的端出现

        private static void SwallowFx(Projectile proj) {
            if (Main.dedServ || fxBudget <= 0 || KikasaDomain.ViewedDreamBlend < 0.3f) {
                return;
            }
            fxBudget--;
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    proj.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    new Color(46, 16, 20) * 0.85f, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(Main.rand.Next(16, 26), 0.012f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                proj.Center, proj.velocity * 0.06f + new Vector2(0f, -0.4f),
                new Color(28, 10, 12) * 0.8f, Main.rand.NextFloat(0.3f, 0.5f))
                ?.Configure(Main.rand.Next(30, 50));
        }
    }

    /// <summary>出生截杀：生成端在 NewProjectile 里就地吞掉，连第一帧都不出现。
    /// 服务器生成的敌弹此处拦不到（服务端无相位），由各端 PreUpdateEntities 在判伤前接手</summary>
    internal class KikasaDreamBanGlobalProj : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (KikasaDreamProjectileBan.AnyDreamWorld
                && KikasaDreamProjectileBan.SwallowedAt(projectile)) {
                KikasaDreamProjectileBan.Swallow(projectile);
            }
        }
    }
}
