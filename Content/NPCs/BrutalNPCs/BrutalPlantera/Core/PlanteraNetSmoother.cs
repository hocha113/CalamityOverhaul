using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>
    /// 客户端位置纠偏，替代原版 <see cref="NPC.netOffset"/> 平滑。
    /// <para>
    /// 原版平滑把每次快照的位置差累进 netOffset，每帧只放掉 2～4 px、上限 300。Boss 战里快照由命中驱动
    /// (服务端每收到一次打击就 netUpdate，节流后约每 4～5 帧一包)，而服务端与客户端的帧相位天然有 ±1 帧
    /// 对齐抖动，每包都带着「一帧位移」量级的位置差：巡航 6～9 px 无所谓，猛扑 46～108 px 就是每包一脚，
    /// 放不完就叠成来回锯齿。原版正是因此把自己所有高速 Boss 列进 NoMultiplayerSmoothingByType；
    /// 世纪之花重制的扑速是原版的六倍以上，却还在享受给慢速 Boss 的平滑。
    /// </para>
    /// <para>
    /// 这里的做法：清掉 netOffset；收包时先用调用方给的帧差把包里位置沿速度投影到本地时钟(抵消对齐抖动)，
    /// 再与本地上一帧的预测位置比较，差得少就保持本帧连续、之后每帧消化一部分；差得多视为瞬移或真失步，直接认服务端
    /// </para>
    /// </summary>
    internal sealed class PlanteraNetSmoother
    {
        /// <summary>超过此距离视为瞬移/真失步，硬对齐不平滑</summary>
        public const float SnapDistance = 160f;
        /// <summary>每帧消化待纠偏量的比例</summary>
        public const float Rate = 0.25f;

        private Vector2 predictedNext;
        private bool hasPrediction;
        /// <summary>尚未消化的纠偏量(服务端位置 - 本地位置)</summary>
        private Vector2 pending;

        /// <summary>AI 开头(客户端)：清原版平滑偏移，分摊一份待消化纠偏</summary>
        public void BeginFrame(NPC npc) {
            npc.netOffset = Vector2.Zero;
            if (pending == Vector2.Zero) {
                return;
            }
            Vector2 step = pending.LengthSquared() < 1f ? pending : pending * Rate;
            npc.position += step;
            pending -= step;
        }

        /// <summary>AI 末尾(客户端)：记下按本地积分预测的下一帧位置(原版随后执行 position += velocity)</summary>
        public void EndFrame(NPC npc) {
            predictedNext = npc.position + npc.velocity;
            hasPrediction = true;
        }

        /// <summary>
        /// 收包时刻(ReceiveExtraAI，此时 position/velocity 已被服务端值覆盖)。
        /// <paramref name="frameDelta"/> 为本地时钟相对这一包的帧差(本地帧数 - 包内帧数)，无法判定时传 0
        /// </summary>
        public void OnSnapshot(NPC npc, int frameDelta) {
            npc.netOffset = Vector2.Zero;
            if (!hasPrediction) {
                return;
            }

            //包里的位置对应服务端发包那一帧，按帧差沿速度推到本地时钟对应的那一帧
            Vector2 serverNow = npc.position + npc.velocity * frameDelta;
            Vector2 error = serverNow - predictedNext;

            if (error.LengthSquared() > SnapDistance * SnapDistance) {
                npc.position = serverNow;
                pending = Vector2.Zero;
                return;
            }

            npc.position = predictedNext;
            pending = error;
        }
    }
}
