using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 调度器。据点（正典）与环境随机（调试）；遭遇互斥在 Materialize。<br/>
    /// 冷却会话级，换世界清零
    /// </summary>
    public sealed class WraithDirector : ModSystem
    {
        /// <summary>规则评估间隔帧</summary>
        public const int CheckIntervalTicks = 60;

        //key → 冷却到期帧
        private static readonly Dictionary<string, long> cooldownUntil = [];
        private static int checkTimer;

        /// <summary>调试闹鬼闸，会话级</summary>
        internal static bool DebugHauntEnabled;

        /// <summary>
        /// 上线闸，目前 false。正典自然渠道经 ContentActiveFor 钳住。<br/>
        /// static readonly 而非 const，避免 CS0162
        /// </summary>
        internal static readonly bool LiveContentEnabled = false;

        /// <summary>正典自然渠道是否放行</summary>
        internal static bool CanonContentActive => LiveContentEnabled || DebugHauntEnabled;

        /// <summary>该定义自然渠道是否放行；调试件豁免</summary>
        internal static bool ContentActiveFor(WraithDefinition definition)
            => CanonContentActive || (definition?.IsDebugContent ?? false);

        public override void ClearWorld() {
            cooldownUntil.Clear();
            checkTimer = 0;
            DebugHauntEnabled = false;
            //据点武装闸同会话态
            Debugs.DebugWraith.DebugSiteArmed = false;
            WraithNet.ClearSession();
        }

        /// <summary>任意厉鬼在场则遭遇中，新显形封锁</summary>
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
            //仅权威调度
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

        /// <summary>据点通道，入圈者过谁以谁触发；事件离场进冷却</summary>
        private static void TrySiteMaterialize(WraithDefinition definition) {
            WraithSitePlan plan = definition.SitePlan;
            if (plan == null || definition.ActorType == null || !ContentActiveFor(definition)) {
                return;
            }

            WraithSiteRecord record = WraithSiteSystem.GetOrCreate(definition.Key);
            long now = (long)Main.GameUpdateCount;

            //事件进行中，离场收账
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

            //动态锚定
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

            //入圈逐人评估
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

        /// <summary>显形，返回 WhoAmI；遭遇互斥+上线闸兜底；仅权威；position=左上角</summary>
        public static int Materialize(WraithDefinition definition, Vector2 position) {
            if (definition?.ActorType == null || VaultUtils.isClient
                || !ContentActiveFor(definition) || EncounterInProgress()) {
                return -1;
            }
            return ActorLoader.NewActor(ActorLoader.GetActorID(definition.ActorType), position);
        }

        /// <summary>在场厉鬼进消散，definition null=全部；仅权威</summary>
        public static void DismissAll(WraithDefinition definition = null) {
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (definition == null || wraith.GetType() == definition.ActorType) {
                    wraith.BeginDematerialize();
                }
            }
        }

        /// <summary>该定义在场实体数</summary>
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
            //蓄水池抽样
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

        /// <summary>默认落点，外围 950~1450px 环带</summary>
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
