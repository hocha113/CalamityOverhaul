using CalamityOverhaul.Content.Scenarios.Kiame.KasaOnis;
using InnoVault.Actors;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.NPCs
{
    /// <summary>
    /// 鬼雨子世界的伞鬼出没调度：这里伞鬼是常驻居民，不是一波演出。<br/>
    /// 权威端（单机本地/子世界服务器）按拍维持每名玩家周边的目标数量，
    /// 远离所有玩家的就地消融回收；生成语境由 <see cref="KasaOniActor"/> 在
    /// OnSpawn 按所在世界自推断（Kiame 内即子世界语境，夺伞下潜不开放）。<br/>
    /// 客户端不做任何裁决，实体乘 Actor 框架的生成广播过线
    /// </summary>
    internal class KiameOniDirector : ModSystem
    {
        //"大量撑伞的鬼"：每名玩家周边的目标在场数与全场上限
        private const int TargetPerPlayer = 8;
        private const int GlobalCap = 24;
        //出没环带：比叠加层更远更散，废村里远远就能看见它们在雨里走
        private const float SpawnRingMin = 360f;
        private const float SpawnRingMax = 980f;
        //统计半径：这个圈里的算"这名玩家周边"
        private const float CountRadius = 1300f;
        //远离所有玩家即溶解回收
        private const float RecycleDistance = 1900f;
        //调度节拍（帧）：每拍至多补两只，人群是渗出来的不是刷出来的
        private const int CheckInterval = 45;
        private const int SpawnPerCheck = 2;

        private static int checkTimer;

        public override void PostUpdateEverything() {
            //生成权威：客户端不做任何裁决（实体乘生成广播过线）
            if (VaultUtils.isClient || !KiameWorld.Active) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;
            MaintainPopulation();
        }

        public override void ClearWorld() => checkTimer = 0;

        private static void MaintainPopulation() {
            List<KasaOniActor> onis = ActorLoader.GetActiveActors<KasaOniActor>();

            //回收：远离所有玩家的就地消融，把名额还给近处
            int total = 0;
            foreach (KasaOniActor oni in onis) {
                total++;
                float nearest = float.MaxValue;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player?.active == true && !player.dead) {
                        nearest = MathHelper.Min(nearest, player.Center.Distance(oni.Center));
                    }
                }
                if (nearest > RecycleDistance) {
                    oni.BeginDespawnDissolve();
                }
            }

            if (total >= GlobalCap) {
                return;
            }

            //逐玩家补员：每拍每人至多两只，渗出来而不是刷出来
            for (int i = 0; i < Main.maxPlayers && total < GlobalCap; i++) {
                Player player = Main.player[i];
                if (player?.active != true || player.dead) {
                    continue;
                }
                int around = CountAround(onis, player.Center);
                int want = System.Math.Min(TargetPerPlayer - around, SpawnPerCheck);
                for (int n = 0; n < want && total < GlobalCap; n++) {
                    if (TryPickSpawnPoint(player, out Vector2 topLeft)) {
                        ActorLoader.NewActor<KasaOniActor>(topLeft);
                        total++;
                    }
                }
            }
        }

        private static int CountAround(List<KasaOniActor> onis, Vector2 center) {
            int count = 0;
            foreach (KasaOniActor oni in onis) {
                if (oni.Center.Distance(center) < CountRadius) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>在玩家两侧环带内探可站立地面；洼地里也照样成形（污水里长出来更对味）</summary>
        private static bool TryPickSpawnPoint(Player player, out Vector2 topLeft) {
            for (int attempt = 0; attempt < 18; attempt++) {
                float distance = Main.rand.NextFloat(SpawnRingMin, SpawnRingMax);
                float dir = Main.rand.NextBool() ? 1f : -1f;
                Vector2 from = new(player.Center.X + dir * distance, player.Center.Y - 220f);
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
    }
}
