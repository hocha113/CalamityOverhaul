using CalamityOverhaul.Content.Scenarios.Kiyume.NPCs;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Stealth
{
    /// <summary>
    /// 嗅迹气味场（P2 点子 13）：玩家奔跑贴地时留下不可见气味足迹，
    /// 寿命 480t（8s）线性衰减到无——年龄即衰减，查询以时间戳论鲜度、寿命内即活点。
    /// 静止/走路不留迹（潜行动词「慢走」因此多一层价值）；
    /// 浓雾不影响气味：嗅觉不是视觉，本场刻意不采样任何雾浓度（设计意图，非疏漏）。<br/>
    /// 环形缓冲是世界级会话状态、只在权威端写读（客户端恒空，恶犬消费全在权威端 AI 路径），
    /// static 合法（同 KiyumeStealthSense 噪声场先例）；零新包——沿迹裁决在服务器，
    /// 客户端从犬的 ai[3] 锚点重放动作。记点方 <see cref="KiyumeStealthPlayer"/>，
    /// 消费方 <see cref="KiyumeHound"/> 搜索态
    /// </summary>
    internal static class KiyumeScentTrail
    {
        private struct ScentPoint
        {
            internal Vector2 Pos;
            internal int Owner;
            internal uint Tick;
            internal bool Live;
        }

        //全玩家共池：192 槽 × 12t 记点节拍 ≈ 单人 38s / 四人各 9.6s 全速奔跑，均盖过 8s 寿命窗
        private static readonly ScentPoint[] ring = new ScentPoint[KiyumeHoundMetrics.ScentRingCapacity];
        private static int cursor;

        /// <summary>记一点（KiyumeStealthPlayer 奔跑贴地路径调用）；客户端调用为无害空转</summary>
        internal static void Record(Vector2 worldPos, int owner) {
            if (VaultUtils.isClient) {
                return;
            }
            ring[cursor] = new ScentPoint {
                Pos = worldPos, Owner = owner, Tick = Main.GameUpdateCount, Live = true,
            };
            cursor = (cursor + 1) % ring.Length;
        }

        /// <summary>
        /// 查半径内最新鲜的活点，返回其位置与主人。恶犬沿迹语义靠反复调用自然成链：
        /// 每次以上一迹点为心再查，最新鲜者即迹链下一点；点寿命同长，
        /// 故半径内活点非当前迹点即必更新，链推进天然单调不回头
        /// </summary>
        internal static bool TryGetFreshScent(Vector2 near, float radiusPx, out Vector2 pos, out int owner) {
            pos = default;
            owner = -1;
            if (radiusPx <= 1f) {
                return false;
            }
            float radiusSq = radiusPx * radiusPx;
            uint bestTick = 0;
            bool found = false;
            for (int i = 0; i < ring.Length; i++) {
                ref ScentPoint p = ref ring[i];
                if (!p.Live) {
                    continue;
                }
                if (Main.GameUpdateCount - p.Tick >= KiyumeHoundMetrics.ScentLifeTicks) {
                    //衰减殆尽顺手清坟（噪声场同款），缩短后续扫描
                    p.Live = false;
                    continue;
                }
                if (Vector2.DistanceSquared(p.Pos, near) > radiusSq) {
                    continue;
                }
                if (!found || p.Tick > bestTick) {
                    found = true;
                    bestTick = p.Tick;
                    pos = p.Pos;
                    owner = p.Owner;
                }
            }
            return found;
        }

        /// <summary>会话复位：ShouldSave=false 每次进梦全新，静态残迹=幽灵气味</summary>
        internal static void ResetSession() {
            Array.Clear(ring);
            cursor = 0;
        }
    }

    //会话复位挂线（镜像 KiyumeStealthSenseSystem 纪律）
    internal class KiyumeScentTrailSystem : ModSystem
    {
        public override void OnWorldLoad() => KiyumeScentTrail.ResetSession();
        public override void OnWorldUnload() => KiyumeScentTrail.ResetSession();
    }
}
