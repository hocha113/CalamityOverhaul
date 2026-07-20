using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 厉鬼调度器：Actor 无持久化，显形实体是"会消失的投影"，由这里在权威端周期评估并重新物化。
    /// 两条通道：据点制（<see cref="WraithSitePlan"/>，正典鬼的唯一出现通道，状态在
    /// <see cref="WraithSiteSystem"/>）与环境随机（<see cref="WraithSpawnRule"/>，仅调试件保留）。<br/>
    /// 全局遭遇互斥（鬼律第七条"同屏一鬼"）：任意厉鬼在场（含过渡态与挣脱体）即封锁一切
    /// 新显形，四条通道（据点/自动/反噬/调试）统一在 <see cref="Materialize"/> 执行本不变量。<br/>
    /// 冷却为会话级，随世界切换清零；外部系统直接显形走 <see cref="Materialize"/>
    /// </summary>
    public sealed class WraithDirector : ModSystem
    {
        /// <summary>规则评估间隔（帧）</summary>
        public const int CheckIntervalTicks = 60;

        //key → 冷却到期的游戏帧
        private static readonly Dictionary<string, long> cooldownUntil = [];
        private static int checkTimer;

        /// <summary>调试闸门：DebugWraith 的自动规则以它为条件，调试物品翻转（会话级，不落档）</summary>
        internal static bool DebugHauntEnabled;

        /// <summary>
        /// 上线闸：厉鬼系统是否面向实际游玩开放。目前**未开放**（用户钦定：完成度不足，
        /// 不给玩家看见）——正典鬼的一切自然渠道（据点调度、环境规则、反噬掷签与生成、借力、
        /// 传闻路标、据点贴饰）统一被 <see cref="ContentActiveFor"/> 钳住；借力键位、
        /// 厉鬼调试器与长命锁物品在闸关时一并不注册/不加载（玩家侧零可见面）。
        /// 系统正式上线时把本开关翻真即可，无需回收各处闸点。
        /// static readonly 而非 const：闸点多为直接 if 判断，避免满仓 CS0162 不可达警告
        /// </summary>
        internal static readonly bool LiveContentEnabled = false;

        /// <summary>正典内容（非调试件）的自然渠道是否放行：上线闸开或调试闹鬼闸开（后者=单人调试专用）</summary>
        internal static bool CanonContentActive => LiveContentEnabled || DebugHauntEnabled;

        /// <summary>
        /// 该定义的自然渠道当前是否放行：正典走 <see cref="CanonContentActive"/>，调试件豁免
        /// （自持调试闸门）。服务器与本端各自判定——调试闸是本端会话态，多人服务器恒关，
        /// 正典内容在多人下天然静默
        /// </summary>
        internal static bool ContentActiveFor(WraithDefinition definition)
            => CanonContentActive || (definition?.IsDebugContent ?? false);

        public override void ClearWorld() {
            cooldownUntil.Clear();
            checkTimer = 0;
            DebugHauntEnabled = false;
            //据点武装闸同为会话态,换世界不许残留(文档"重进需重开"的执行点)
            Debugs.DebugWraith.DebugSiteArmed = false;
            WraithNet.ClearSession();
        }

        /// <summary>
        /// 任意厉鬼在场（含过渡态与挣脱体）——遭遇进行中，新显形一律封锁。
        /// 零分配热判定：活跃数 O(1) 先剪，非空才扫槽位数组（框架稠密表不对外，槽扫已是最省公开路径）
        /// </summary>
        public static bool EncounterInProgress() {
            if (ActorLoader.GetActiveActorCount() <= 0) {
                return false;
            }
            Actor[] actors = ActorLoader.Actors;
            for (int i = 0; i < actors.Length; i++) {
                if (actors[i] is WraithActor { Active: true }) {
                    return true;
                }
            }
            return false;
        }

        public override void PostUpdateEverything() {
            //调度只在权威端跑,客户端实体由生成广播带来
            if (VaultUtils.isClient || Main.gameMenu) {
                return;
            }
            if (++checkTimer < CheckIntervalTicks) {
                return;
            }
            checkTimer = 0;

            foreach (WraithDefinition definition in WraithRegistry.All) {
                TryAutoMaterialize(definition);
                TrySiteMaterialize(definition);
            }
        }

        /// <summary>
        /// 据点通道：锚定 → 冷却判定 → 对每名实际入圈玩家逐人评估活化条件，条件过谁就以谁触发
        /// （评估者=触发者，不再随机抽人评估）。一场事件 = 该据点实体自显形到离场（无论何种退场），
        /// 随后进入冷却
        /// </summary>
        private static void TrySiteMaterialize(WraithDefinition definition) {
            WraithSitePlan plan = definition.SitePlan;
            if (plan == null || definition.ActorType == null || !ContentActiveFor(definition)) {
                return;
            }

            WraithSiteRecord record = WraithSiteSystem.GetOrCreate(definition.Key);
            long now = (long)Main.GameUpdateCount;

            //事件进行中:盯着实体离场,离场即收账进冷却
            if (record.ActiveWhoAmI >= 0) {
                Actor actor = ActorLoader.Actors[record.ActiveWhoAmI];
                bool alive = actor != null && actor.Active
                    && actor.Generation == record.ActiveGeneration
                    && actor.GetType() == definition.ActorType;
                if (alive) {
                    return;
                }
                record.ActiveWhoAmI = -1;
                record.EventCount++;
                record.CooldownUntil = now + plan.CooldownTicks;
                return;
            }

            //动态锚定:未锚定且有选点器,按重试节流尝试;选点参照人取随机存活玩家
            if (!record.Anchored) {
                if (plan.AnchorPicker == null || now < record.NextAnchorRetry) {
                    return;
                }
                Player scout = PickCandidatePlayer();
                if (scout == null) {
                    return;
                }
                record.NextAnchorRetry = now + plan.AnchorRetryTicks;
                Vector2? anchor = plan.AnchorPicker(new WraithSiteContext {
                    Definition = definition,
                    Candidate = scout,
                });
                if (anchor == null) {
                    return;
                }
                record.Anchor = anchor.Value;
                record.Anchored = true;
            }

            if (now < record.CooldownUntil) {
                return;
            }

            //入圈者逐人评估:活化条件对"实际将进入据点的玩家"判定,过谁以谁触发
            Player trigger = null;
            float triggerSq = plan.TriggerRadius * plan.TriggerRadius;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || Vector2.DistanceSquared(player.Center, record.Anchor) >= triggerSq) {
                    continue;
                }
                if (plan.ActivationCondition != null && !plan.ActivationCondition(new WraithSiteContext {
                    Definition = definition,
                    Candidate = player,
                    Anchor = record.Anchor,
                })) {
                    continue;
                }
                trigger = player;
                break;
            }
            if (trigger == null) {
                return;
            }

            Vector2 topLeft = record.Anchor - new Vector2(definition.HitboxWidth * 0.5f, definition.HitboxHeight * 0.5f);
            int whoAmI = Materialize(definition, topLeft);
            if (whoAmI >= 0) {
                record.ActiveWhoAmI = whoAmI;
                record.ActiveGeneration = ActorLoader.Actors[whoAmI].Generation;
            }
        }

        private static void TryAutoMaterialize(WraithDefinition definition) {
            WraithSpawnRule rule = definition.SpawnRule;
            if (rule == null || definition.ActorType == null || !ContentActiveFor(definition)) {
                return;
            }

            long now = (long)Main.GameUpdateCount;
            if (cooldownUntil.TryGetValue(definition.Key, out long until) && now < until) {
                return;
            }
            if (CountAlive(definition) >= rule.MaxAlive) {
                return;
            }

            Player candidate = PickCandidatePlayer();
            if (candidate == null) {
                return;
            }

            WraithSpawnContext context = new() { Player = candidate, Definition = definition };
            if (rule.Condition != null && !rule.Condition(context)) {
                return;
            }
            if (Main.rand.NextFloat() >= MathHelper.Clamp(rule.ChancePerCheck, 0f, 1f)) {
                return;
            }

            Vector2? position = rule.PositionPicker != null ? rule.PositionPicker(context) : PickDefaultPosition(context);
            if (position == null) {
                return;
            }

            if (Materialize(definition, position.Value) >= 0) {
                cooldownUntil[definition.Key] = now + rule.CooldownTicks;
            }
        }

        /// <summary>
        /// 显形一只厉鬼，返回实体 WhoAmI。全局遭遇互斥在此执行：已有厉鬼在场直接放弃（返回 -1），
        /// 一切生成通道共用本闸；上线闸（<see cref="ContentActiveFor"/>）同在此兜底——
        /// 系统未开放期间任何旁路都物化不出正典鬼。仅权威端可生成；客户端调用一律返回 -1
        /// （调试通道在多人下由调试器明示不受理，不发生成请求）。position 为实体左上角（Actor.Position 语义）
        /// </summary>
        public static int Materialize(WraithDefinition definition, Vector2 position) {
            if (definition?.ActorType == null || VaultUtils.isClient
                || !ContentActiveFor(definition) || EncounterInProgress()) {
                return -1;
            }
            return ActorLoader.NewActor(ActorLoader.GetActorID(definition.ActorType), position);
        }

        /// <summary>让在场厉鬼进入消散，definition 为 null 时波及全部；仅权威端有效</summary>
        public static void DismissAll(WraithDefinition definition = null) {
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (definition == null || wraith.GetType() == definition.ActorType) {
                    wraith.BeginDematerialize();
                }
            }
        }

        /// <summary>该定义当前在场（含过渡中）的实体数</summary>
        public static int CountAlive(WraithDefinition definition) {
            int count = 0;
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (wraith.GetType() == definition.ActorType) {
                    count++;
                }
            }
            return count;
        }

        private static Player PickCandidatePlayer() {
            //蓄水池抽样等概率挑一名存活玩家,免建列表
            Player picked = null;
            int seen = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                seen++;
                if (Main.rand.NextBool(seen)) {
                    picked = player;
                }
            }
            return picked;
        }

        /// <summary>
        /// 默认落点：候选玩家外围 950~1450px 环带（大抵在屏幕外缘），避开实体物块与世界边缘，
        /// 多次尝试全失败则本轮放弃
        /// </summary>
        private static Vector2? PickDefaultPosition(WraithSpawnContext context) {
            int width = context.Definition.HitboxWidth;
            int height = context.Definition.HitboxHeight;
            for (int attempt = 0; attempt < 10; attempt++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(950f, 1450f);
                Vector2 center = context.Player.Center + angle.ToRotationVector2() * dist;
                Vector2 topLeft = center - new Vector2(width * 0.5f, height * 0.5f);
                if (topLeft.X < 800f || topLeft.X > Main.maxTilesX * 16f - 800f
                    || topLeft.Y < 800f || topLeft.Y > Main.maxTilesY * 16f - 800f) {
                    continue;
                }
                if (Collision.SolidCollision(topLeft, width, height)) {
                    continue;
                }
                return topLeft;
            }
            return null;
        }
    }
}
