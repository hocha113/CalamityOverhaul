using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Ambience
{
    /// <summary>
    /// 海沟环境粒子总线(C 路):随机探针制海雪发射 + 远景生物微光调度。
    /// 纯客户端表现层,零网络包、零 tile 写入,各端探针随机独立
    /// (镜像 DungeonworldAmbientFX 口径)。presence 消费 <see cref="HadalAmbience.Presence"/>,
    /// 非激活零开销
    /// </summary>
    internal class HadalAmbientFX : ModSystem
    {
        //==== Debug 静态口(TestItem 验收用) ====
        /// <summary>发射概率热调(0~3;每 tick 硬帽不受影响,只能降不能破帽)</summary>
        internal static float RateMul = 1f;

        //每 tick 探针数:屏窗内均匀随机,纯读,亚微秒级/针
        private const int ProbesPerTick = 24;
        //海雪每 tick 生成硬帽(常驻发射的数学上界:2×寿命 205f 均值 → 存活远低于类型帽)
        private const int SnowCapPerTick = 2;

        //微光调度(帧)
        private const int GleamPeriodMin = 1080;
        private const int GleamPeriodMax = 2700;
        private const int GleamRetryDelay = 420;
        //微光出生亮度门:比这亮的位置不算"黑暗深处"
        private const float GleamDarkGate = 0.12f;

        private static int snowSpent;
        private static int gleamTimer;

        //海雪色:冷白偏蓝的悬浮碎屑(光照门控后呈现为灯光里的浮尘)
        private static readonly Color SnowPale = new(205, 218, 228);
        //微光色族:苍青/幽绿,潜渊症深海生物光的家族色
        private static readonly Color GleamCyan = new(140, 215, 225);
        private static readonly Color GleamGreen = new(115, 200, 170);

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        public override void Unload() {
            HardReset();
            RateMul = 1f;
        }

        private static void HardReset() {
            snowSpent = 0;
            gleamTimer = 0;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            float presence = HadalAmbience.Presence;
            if (presence < 0.02f || Main.gameMenu) {
                return;
            }

            snowSpent = 0;
            RunProbes(presence);
            UpdateGleam(presence);
        }

        //==================== 海雪探针 ====================

        private static void RunProbes(float presence) {
            int left = (int)(Main.screenPosition.X / 16f) - 6;
            int top = (int)(Main.screenPosition.Y / 16f) - 6;
            int right = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + 6;
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 6;
            left = (int)MathHelper.Clamp(left, 1, Main.maxTilesX - 2);
            right = (int)MathHelper.Clamp(right, left + 1, Main.maxTilesX - 2);
            top = (int)MathHelper.Clamp(top, 1, Main.maxTilesY - 2);
            bottom = (int)MathHelper.Clamp(bottom, top + 1, Main.maxTilesY - 2);

            float mul = presence * MathHelper.Clamp(RateMul, 0f, 3f);

            for (int i = 0; i < ProbesPerTick && snowSpent < SnowCapPerTick; i++) {
                int x = Main.rand.Next(left, right);
                int y = Main.rand.Next(top, bottom);
                if (y < HadalworldMetrics.SeaLevelRow) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(x, y);
                //只在真水体里发射(气穴/实心不生雪)
                if (tile.LiquidAmount < 128 || (tile.HasTile && Main.tileSolid[tile.TileType])) {
                    continue;
                }
                float p = HadalDepthProfile.Sample(HadalworldMetrics.DepthFraction(y * 16f)).Snow * mul;
                if (p <= 0f || Main.rand.NextFloat() >= p) {
                    continue;
                }
                snowSpent++;
                Vector2 px = new(x * 16f + 8f, y * 16f + 8f);
                PRTLoader.NewParticle<PRT_HadalSnow>(px + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(0.08f, 0.04f), SnowPale,
                    Main.rand.NextFloat(0.045f, 0.105f))
                    ?.Configure(Main.rand.Next(150, 260), Main.rand.NextFloat(0.09f, 0.22f));
            }
        }

        //==================== 远景生物微光 ====================

        private static void UpdateGleam(float presence) {
            if (gleamTimer > 0) {
                gleamTimer--;
                return;
            }

            float frac = HadalworldMetrics.DepthFraction(HadalAmbience.CurrentRow() * 16f);
            float weight = HadalDepthProfile.GleamWeight(frac) * presence;
            if (weight < 0.05f || Main.rand.NextFloat() > weight) {
                gleamTimer = GleamRetryDelay;
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                gleamTimer = GleamRetryDelay;
                return;
            }

            //远景落点:玩家 14~34 tile 外随机方位,试 6 次,要求真水体+黑暗
            for (int attempt = 0; attempt < 6; attempt++) {
                Vector2 dir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                Vector2 pos = player.Center + dir * Main.rand.NextFloat(14f, 34f) * 16f;
                Tile tile = HadalPRTUtil.SafeTile(pos);
                if (tile.LiquidAmount < 128 || (tile.HasTile && Main.tileSolid[tile.TileType])) {
                    continue;
                }
                if (HadalPRTUtil.SafeBright(pos) > GleamDarkGate) {
                    continue;
                }
                SpawnGleamCluster(pos);
                gleamTimer = Main.rand.Next(GleamPeriodMin, GleamPeriodMax);
                return;
            }
            gleamTimer = GleamRetryDelay;
        }

        //单点为主,三成概率 2~3 粒小簇同向缓移(像一串鱼灯掠过又不像)
        private static void SpawnGleamCluster(Vector2 pos) {
            Color color = Main.rand.NextBool(3) ? GleamGreen : GleamCyan;
            Vector2 drift = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                * Main.rand.NextFloat(0.06f, 0.20f);
            int count = Main.rand.NextBool(3, 10) ? Main.rand.Next(2, 4) : 1;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_HadalGleam>(pos + Main.rand.NextVector2Circular(40f, 26f),
                    drift * Main.rand.NextFloat(0.85f, 1.15f), color,
                    Main.rand.NextFloat(0.05f, 0.10f))
                    ?.Configure(Main.rand.Next(260, 430), Main.rand.NextFloat(0.020f, 0.045f));
            }
        }

        /// <summary>一行状态摘要(TestItem 验收用)</summary>
        internal static string StatusLine() {
            int snow = 0;
            int gleam = 0;
            var inds = PRTLoader.PRT_InGame_World_Inds;
            if (inds != null) {
                foreach (var prt in inds) {
                    if (prt == null || !prt.active) {
                        continue;
                    }
                    if (prt is PRT_HadalSnow) {
                        snow++;
                    }
                    else if (prt is PRT_HadalGleam) {
                        gleam++;
                    }
                }
            }
            return $"[海沟粒子] presence{HadalAmbience.Presence:F2} 海雪{snow} 微光{gleam}"
                + $" 微光计时{gleamTimer} RateMul{RateMul:F2}";
        }
    }
}
