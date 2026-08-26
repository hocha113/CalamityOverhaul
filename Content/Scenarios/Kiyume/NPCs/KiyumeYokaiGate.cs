using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 百鬼整树加载闸门，改 Enabled 即重新加载（镜像 DungeonworldEliteGate）。
    /// 裁决15：初值 true——入口仍是 DEBUG /kiyume，生产不可达，直接开 content 便于验收
    /// </summary>
    internal static class KiyumeYokaiGate
    {
        internal const bool Enabled = true;
    }

    /// <summary>
    /// 百鬼公共基类（镜像 EliteModNPC 全套合同，按鬼梦语境重命名，不 import 深牢类型）：
    /// 只做加载门禁 + 四槽 ai 命名 + 出生 alpha 自愈 + 节拍前进沿工具 + 鬼梦公共合同，不藏行为。
    /// 联机合同：状态机转移只在服务器（ChangeState），ai[0..3] 乘 SyncNPC 过线；
    /// 表现各端本地由 ai 推导；音效走严格前进沿防回卷重播。
    /// 鬼梦追加合同（P4 计划书 §2.0）：AI 首行梦外自杀、不因距离退场、
    /// 图鉴一律隐藏（裁决14）、无赏金（value=0）——全部由 sealed 钩子统一强制，子类只填虚挂点。
    /// <para/>localAI 槽位分配：[0]=本地环境钟 [1]=服务器周期同步步调 [2]=已观察状态(+1) [3]=当前状态已播最高节拍
    /// </summary>
    internal abstract class KiyumeYokaiNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => KiyumeYokaiGate.Enabled;

        /// <summary>ai[0]：状态机状态（默认槽位约定；自定义布局的怪直接用 NPC.ai[]，绕开这些命名）</summary>
        protected ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态内计时</summary>
        protected ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：状态参数（各怪自定义语义）</summary>
        protected ref float StateParam => ref NPC.ai[2];
        /// <summary>ai[3]：层叠计数（跟随计量/编队位/样式种子等各怪自定义）</summary>
        protected ref float StackCount => ref NPC.ai[3];

        /// <summary>本地表现钟（不入同步）</summary>
        protected ref float AmbientClock => ref NPC.localAI[0];

        /// <summary>每实体确定相位种子（连续值纪律）</summary>
        protected float Seed => NPC.whoAmI * 0.7391f;

        //==================== 生命周期合同（sealed 收口 + 虚挂点） ====================

        /// <summary>图鉴一律隐藏（裁决14：梦不入图鉴，镜像 OldNet 敌人惯例）；子类静态默认走 SetYokaiStaticDefaults</summary>
        public sealed override void SetStaticDefaults() {
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            SetYokaiStaticDefaults();
        }

        /// <summary>子类静态默认值（npcFrameCount / AnimationType / MustAlwaysDraw 等）</summary>
        protected virtual void SetYokaiStaticDefaults() { }

        /// <summary>默认值收口：梦中怪一律无赏金（value=0 统一强制）；子类默认值走 SetYokaiDefaults</summary>
        public sealed override void SetDefaults() {
            SetYokaiDefaults();
            NPC.value = 0f;
        }

        /// <summary>子类默认值（尺寸/血防/aiStyle=-1 等）</summary>
        protected abstract void SetYokaiDefaults();

        /// <summary>导演布防的受控体不参与原版远离退场（梦外自杀是唯一离场兜底，镜像 Lurker）</summary>
        public sealed override bool CheckActive() => false;

        /// <summary>鬼梦门控：绝不泄漏到主世界与其他子世界（镜像 Lurker 门控）；行为体走 YokaiAI</summary>
        public sealed override void AI() {
            if (!KiyumeWorld.Active) {
                NPC.active = false;
                return;
            }
            YokaiAI();
        }

        /// <summary>各怪行为体（已在鬼梦门控之内）</summary>
        protected abstract void YokaiAI();

        //==================== 联机工具（镜像 EliteModNPC 逐项） ====================

        /// <summary>服务器裁决状态转移；各端表现由 ai 重放，转移点必置 netUpdate（默认槽位布局适用）</summary>
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
        /// 节拍严格前进沿：同状态内 beat 单调递增才触发一次；
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
        /// 服务器周期同步锚：自写步态没有原版 AI 的双端等同模拟兜底，
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

        //==================== 统一现形语法（§2.0：alpha 系数 = 距离项 × 雾浓度项） ====================

        /// <summary>
        /// 雾浓度项：浓度归一 (DensityAt−0.28)/0.26，采样点抬离脚 24px
        /// （数值同源 KiyumeHoundShade.Advance 的硬编码，改一处请对照另一处；常量在 KiyumeYokaiMetrics）
        /// </summary>
        protected static float FogRevealTerm(Vector2 worldPos) {
            float density = KiyumeFogSim.DensityAt(
                worldPos - new Vector2(0f, KiyumeYokaiMetrics.RevealFogLiftPx));
            return MathHelper.Clamp(
                (density - KiyumeYokaiMetrics.RevealFogFloor) / KiyumeYokaiMetrics.RevealFogSpan, 0f, 1f);
        }

        /// <summary>距离项：near 内 0、far 外 1 线性归一（各怪传自己的带宽）</summary>
        protected static float DistanceRevealTerm(float dist, float nearPx, float farPx) {
            return MathHelper.Clamp((dist - nearPx) / (farPx - nearPx), 0f, 1f);
        }

        //==================== SpriteBatch 批切换（镜像 EliteModNPC） ====================

        /// <summary>切入加色批。加色批源因子=SrcAlpha：强度必须写进色值整体（color * k），禁 A=0</summary>
        protected static void BeginAdditive(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>还原默认批（同时充当对上游批状态泄漏的复位）</summary>
        protected static void BeginDefault(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
