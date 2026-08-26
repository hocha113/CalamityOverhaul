using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>地牢精英怪内容树总门禁（2026-08-27 重做批交付开启，镜像 DeepGaolWraithGate）；关闭 Enabled 即整树下线</summary>
    internal static class DungeonworldEliteGate
    {
        internal const bool Enabled = true;
    }

    /// <summary>
    /// 精英怪公共基类：只做加载门禁 + 四槽 ai 命名 + 出生 alpha 自愈 + 节拍前进沿工具，
    /// 不藏行为。联机合同（WAVE2-ENEMIES §3.0）：状态机转移只在服务器（ChangeState），
    /// ai[0..3] 乘 SyncNPC 过线；表现各端本地由 ai 推导；音效走严格前进沿防回卷重播。
    /// <para/>localAI 槽位分配：[0]=本地环境钟 [1]=服务器周期同步步调 [2]=已观察状态(+1) [3]=当前状态已播最高节拍
    /// </summary>
    internal abstract class EliteModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => DungeonworldEliteGate.Enabled;

        /// <summary>ai[0]：状态机状态</summary>
        protected ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态内计时</summary>
        protected ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：状态参数（各怪自定义语义）</summary>
        protected ref float StateParam => ref NPC.ai[2];
        /// <summary>ai[3]：层叠计数（警报冷却/进食数/搁浅累计/骨料囤积）</summary>
        protected ref float StackCount => ref NPC.ai[3];

        /// <summary>本地表现钟（不入同步）</summary>
        protected ref float AmbientClock => ref NPC.localAI[0];

        /// <summary>每实体确定相位种子（法 9.1 连续值纪律）</summary>
        protected float Seed => NPC.whoAmI * 0.7391f;

        /// <summary>服务器裁决状态转移；各端表现由 ai 重放，转移点必置 netUpdate</summary>
        protected void ChangeState(int state, float param = 0f) {
            State = state;
            StateTimer = 0f;
            StateParam = param;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 出生 alpha 自愈：AI 首行各端无条件收敛到状态目标值。
        /// 目标是状态的确定函数，不写一次性 alpha-= 事件，任何端中途进场即收敛，无隐形窗口
        /// </summary>
        protected void HealAlpha(int target, int step = 12) {
            if (NPC.alpha > target) {
                NPC.alpha = Math.Max(target, NPC.alpha - step);
            }
            else if (NPC.alpha < target) {
                NPC.alpha = Math.Min(target, NPC.alpha + step);
            }
        }

        /// <summary>
        /// 各端本地观察 ai[0] 变化：状态切换沿返回 true 并重置本状态节拍线。
        /// 迟入端第一帧也会得到一次切换沿（localAI 初值 0 ≠ 任何状态+1）
        /// </summary>
        protected bool StateEdge() {
            if ((int)NPC.localAI[2] == (int)State + 1) {
                return false;
            }
            NPC.localAI[2] = (int)State + 1;
            NPC.localAI[3] = 0f;
            return true;
        }

        /// <summary>
        /// 节拍严格前进沿（netcode 7.5）：同状态内 beat 单调递增才触发一次；
        /// 权威快照回卷计时后不会重播已放过的音效/粒子
        /// </summary>
        protected bool BeatForward(int beat) {
            if (beat <= (int)NPC.localAI[3]) {
                return false;
            }
            NPC.localAI[3] = beat;
            return true;
        }

        /// <summary>
        /// 服务器周期同步锚（netcode 3.2）：自写步态没有原版 AI 的双端等同模拟兜底，
        /// 低频重发 SyncNPC 把客户端位置漂移钳在一拍以内
        /// </summary>
        protected void ServerSyncPacer(int interval = 24) {
            if (!VaultUtils.isServer) {
                return;
            }
            if (++NPC.localAI[1] >= interval) {
                NPC.localAI[1] = 0f;
                NPC.netUpdate = true;
            }
        }

        //==================== SpriteBatch 批切换（镜像 GaolDormantSkull.DrawGlow）====================

        /// <summary>切入加色批。加色批源因子=SrcAlpha：强度必须写进色值整体（color * k），禁 A=0</summary>
        protected static void BeginAdditive(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>还原默认批（同时充当对上游批状态泄漏的复位，netcode 7.2）</summary>
        protected static void BeginDefault(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
