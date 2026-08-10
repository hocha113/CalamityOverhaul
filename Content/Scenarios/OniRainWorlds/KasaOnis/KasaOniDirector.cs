using InnoVault.Actors;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis
{
    /// <summary>
    /// 伞鬼的本地出没调度：盯着本机玩家的鬼雨深度，
    /// 初入第一层（含带档回雨）在四周布一圈凝聚生成，浮出雨世界时清场。<br/>
    /// 多人下生成与销毁都走 Actor 框架的客户端请求，权威在服务器；
    /// 单机退场由 <see cref="KasaOniActor"/> 权威自检兜底，这里是同帧的显式入口。
    /// </summary>
    internal static class KasaOniDirector
    {
        private const int SpawnCountMin = 3;
        private const int SpawnCountMax = 5;
        private const float SpawnRingMin = 300f;
        private const float SpawnRingMax = 650f;
        private const int MaxPerOwner = 6;
        private const int StaggerMin = 26;
        private const int StaggerMax = 55;

        private static int prevDepth;
        private static bool pendingSpawn;
        private static int pendingRemaining;
        private static int pendingStagger;

        /// <summary>每帧驱动，仅非专用服务器调用（深度是本地量）</summary>
        internal static void Update() {
            if (Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            int depth = OniRainWorldState.LocalDepth;
            if (depth > 0 && prevDepth == 0) {
                //初入雨世界（撑伞入雨或带档载入），备一圈凝聚生成
                pendingSpawn = true;
                pendingRemaining = Main.rand.Next(SpawnCountMin, SpawnCountMax + 1);
                pendingStagger = 0;
            }
            else if (depth == 0 && prevDepth > 0) {
                pendingSpawn = false;
                DespawnFor(player);
            }
            prevDepth = depth;

            if (pendingSpawn) {
                UpdatePendingSpawns(player);
            }
        }

        private static void UpdatePendingSpawns(Player player) {
            //入雨/深潜演出未收尾不抢戏，玩家没活过来也等着
            if (OniRainWorldTransition.Active || OniRainDescentTransition.Active
                || !player.Alives()) {
                return;
            }
            if (!OniRainWorldState.LocalIn) {
                pendingSpawn = false;
                return;
            }
            if (--pendingStagger > 0) {
                return;
            }

            if (CountOwnedBy(player.whoAmI) >= MaxPerOwner) {
                pendingSpawn = false;
                return;
            }

            if (TryPickSpawnPoint(player, out Vector2 topLeft)) {
                //多人客户端此调用只发生成请求并返回 -1，由服务器分配槽位后广播
                ActorLoader.NewActor<KasaOniActor>(topLeft);
            }
            //探不到地也消耗名额，防止在恶劣地形上无限重试
            pendingRemaining--;
            pendingStagger = Main.rand.Next(StaggerMin, StaggerMax);
            if (pendingRemaining <= 0) {
                pendingSpawn = false;
            }
        }

        /// <summary>玩家浮出雨世界：清掉属于他的伞鬼</summary>
        private static void DespawnFor(Player player) {
            foreach (KasaOniActor oni in ActorLoader.GetActiveActors<KasaOniActor>()) {
                bool mine = oni.OwnerWhoAmI == player.whoAmI || oni.OwnerWhoAmI < 0;
                if (!mine) {
                    continue;
                }
                if (VaultUtils.isClient) {
                    //观察者已出雨（看不见它们），直接请求服务器销毁
                    ActorLoader.KillActor(oni.WhoAmI);
                }
                else {
                    //单机/Host&Play 权威端：走消融演出退场
                    oni.BeginDespawnDissolve();
                }
            }
        }

        private static int CountOwnedBy(int whoAmI) {
            int count = 0;
            foreach (KasaOniActor oni in ActorLoader.GetActiveActors<KasaOniActor>()) {
                if (oni.OwnerWhoAmI == whoAmI) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>在玩家两侧 300~650px 环带内探可站立地面</summary>
        private static bool TryPickSpawnPoint(Player player, out Vector2 topLeft) {
            for (int attempt = 0; attempt < 20; attempt++) {
                float distance = Main.rand.NextFloat(SpawnRingMin, SpawnRingMax);
                float dir = Main.rand.NextBool() ? 1f : -1f;
                Vector2 from = new(player.Center.X + dir * distance, player.Center.Y - 200f);
                if (KasaOniActor.TryFindStandableGround(from,
                    KasaOniActor.HitboxWidth, KasaOniActor.HitboxHeight, out Vector2 feet)) {
                    topLeft = feet - new Vector2(KasaOniActor.HitboxWidth * 0.5f,
                        KasaOniActor.HitboxHeight);
                    return true;
                }
            }
            topLeft = default;
            return false;
        }

        internal static void ResetLocal() {
            prevDepth = 0;
            pendingSpawn = false;
            pendingRemaining = 0;
            pendingStagger = 0;
        }

        /// <summary>调试放鬼：鼠标处向下吸附地面凝出一只（多人客户端自动转生成请求）</summary>
        internal static void DebugSpawnAt(Vector2 world) {
            if (!KasaOniActor.TryFindStandableGround(world - new Vector2(0f, 60f),
                KasaOniActor.HitboxWidth, KasaOniActor.HitboxHeight, out Vector2 feet)) {
                Main.NewText("此处探不到可站立地面", Color.IndianRed);
                return;
            }
            ActorLoader.NewActor<KasaOniActor>(feet - new Vector2(
                KasaOniActor.HitboxWidth * 0.5f, KasaOniActor.HitboxHeight));
        }
    }
}
