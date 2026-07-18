using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 厉鬼调度器：Actor 无持久化，显形实体是"会消失的投影"，
    /// 由这里在权威端按各定义的 <see cref="WraithSpawnRule"/> 周期评估并重新物化。
    /// 冷却为会话级，随世界切换清零；外部系统直接显形走 <see cref="Materialize"/>
    /// </summary>
    public sealed class WraithDirector : ModSystem
    {
        /// <summary>规则评估间隔（帧）</summary>
        public const int CheckIntervalTicks = 60;

        //key → 冷却到期的游戏帧
        private static readonly Dictionary<string, long> cooldownUntil = [];
        private static int checkTimer;

        /// <summary>调试闸门：DebugWraith 的自动规则以它为条件，调试物品右键翻转（会话级，不落档）</summary>
        internal static bool DebugHauntEnabled;

        public override void ClearWorld() {
            cooldownUntil.Clear();
            checkTimer = 0;
            DebugHauntEnabled = false;
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
            }
        }

        private static void TryAutoMaterialize(WraithDefinition definition) {
            WraithSpawnRule rule = definition.GetSpawnRule();
            if (rule == null || definition.ActorType == null) {
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
        /// 显形一只厉鬼，返回实体 WhoAmI。客户端调用会转为向服务器请求并返回 -1，
        /// 不计入自动冷却。position 为实体左上角（Actor.Position 语义）
        /// </summary>
        public static int Materialize(WraithDefinition definition, Vector2 position) {
            if (definition?.ActorType == null) {
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
