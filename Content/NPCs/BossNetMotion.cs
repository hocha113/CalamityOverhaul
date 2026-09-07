using System;
using System.IO;
using Terraria;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// 能随快照过线的状态：各 Boss 的状态基类实现它，控制器便无需知道具体状态类型。
    /// <c>StateId</c>/<c>Timer</c>/<c>Counter</c> 由 <c>VaultState</c> 自带，只需补收养方法
    /// </summary>
    internal interface IBossNetTiming
    {
        int StateId { get; }
        int Timer { get; }
        int Counter { get; }
        /// <summary>客户端：收养权威端的计时（带容差，见 <see cref="BossNetMotion.AdoptTimer"/>）</summary>
        void AdoptNetTiming(int timer, int counter);
    }

    /// <summary>
    /// Boss 联机运动共用件：客户端位置纠偏 + 状态机计时随快照过线。
    /// <para>
    /// 原版 <see cref="NPC.netOffset"/> 平滑把每次快照的位置差累进偏移量，每帧只放掉 2～4 px、上限 300。
    /// Boss 战里快照由命中驱动（服务端每收到一次打击就 netUpdate，节流后约每 4～5 帧一包），
    /// 两端帧相位又天然带 ±1 帧对齐抖动，于是每包都带着「一帧位移」量级的位置差：
    /// 巡航几 px 无所谓，冲刺 40～100 px 就是每包一脚，放不完便叠成来回锯齿。
    /// 原版正因此把自己所有高速 Boss 列进 NoMultiplayerSmoothingByType/ByAI。
    /// </para>
    /// <para>
    /// 本件的做法：清掉 netOffset 自己接管。收包时先按帧差把包里位置沿速度投影到本地时钟，
    /// 再跟本地上一帧的预测位置比较：差得少就保持本帧连续、之后每帧消化一部分；
    /// 差得多视为瞬移或真失步，直接认服务端。速度一律采用包里的值。
    /// </para>
    /// <para>
    /// 另一半是计时。<c>VaultState.Timer/Counter</c> 是纯本地量，客户端换态时新实例从 0 起跑，
    /// 同一套运动数学便从不同起点推进——锁向帧、发射拍、刹车拍全都错开，那是真失步而非抖动。
    /// 所以权威端把当前状态的计时随快照同包送出（<c>SendExtraAI</c> 与位置/速度原子到达），
    /// 客户端带容差收养：只差一两帧属网络抖动常态，硬对齐反而会让 <c>Timer == X</c> 型一次性拍被跳过或重放。
    /// </para>
    /// </summary>
    internal sealed class BossNetMotion
    {
        /// <summary>超过此距离视为瞬移/真失步，硬对齐不平滑</summary>
        public const float SnapDistance = 160f;
        /// <summary>每帧消化待纠偏量的比例</summary>
        public const float Rate = 0.25f;
        /// <summary>帧差容差：超出即认定不是相位抖动，不做投影</summary>
        public const int MaxFrameDelta = 2;
        /// <summary>计时收养容差，同上口径</summary>
        public const int TimerTolerance = 2;
        /// <summary>兜底心跳间隔：确定性积分后两端只差几像素，慢频足够对账</summary>
        public const int HeartbeatFrames = 45;

        private Vector2 predictedNext;
        private bool hasPrediction;
        /// <summary>尚未消化的纠偏量（服务端位置 - 本地位置）</summary>
        private Vector2 pending;

        private bool timingPending;
        private int packetStateId = -1;
        private int packetTimer;
        private int packetCounter;
        private bool swayPending;
        private float packetSway;

        /// <summary>客户端 AI 开头：清原版平滑偏移，分摊一份待消化纠偏</summary>
        public void BeginFrame(NPC npc) {
            npc.netOffset = Vector2.Zero;
            if (pending == Vector2.Zero) {
                return;
            }
            Vector2 step = pending.LengthSquared() < 1f ? pending : pending * Rate;
            npc.position += step;
            pending -= step;
        }

        /// <summary>客户端 AI 末尾：记下按本地积分预测的下一帧位置（原版随后执行 position += velocity）</summary>
        public void EndFrame(NPC npc) {
            predictedNext = npc.position + npc.velocity;
            hasPrediction = true;
        }

        /// <summary>状态直接改写位置（瞬移/破土定位）后调用：丢掉旧预测，下一包不当失步处理</summary>
        public void ForgetPrediction() {
            hasPrediction = false;
            pending = Vector2.Zero;
        }

        /// <summary>
        /// 收包时刻（<c>ReceiveExtraAI</c>/<c>NetReceive</c>，此时 position/velocity 已被服务端值覆盖）。
        /// <paramref name="frameDelta"/> 为本地时钟相对这一包的帧差（本地帧数 - 包内帧数），无法判定时传 0
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

        /// <summary>
        /// 权威端：把当前状态的计时与摆动相位写进快照流（<c>SendExtraAI</c> / <c>NetSend</c> 内调用）。
        /// <paramref name="sway"/> 是持久累加型的相位（蛇行/摆尾），没有的传 0；
        /// 格式固定在这一处，两端才不会读写错位
        /// </summary>
        public static void WriteTiming(BinaryWriter writer, int stateId, int timer, int counter, float sway = 0f) {
            writer.Write(stateId);
            writer.Write(timer);
            writer.Write(counter);
            writer.Write(sway);
        }

        /// <summary>
        /// 客户端：读出权威端计时并顺手纠偏（<c>ReceiveExtraAI</c> 内调用，与 <see cref="WriteTiming"/> 严格对应）
        /// </summary>
        public void ReceiveTiming(BinaryReader reader, NPC npc, int localStateId, int localTimer) {
            int stateId = reader.ReadInt32();
            int timer = reader.ReadInt32();
            int counter = reader.ReadInt32();
            float sway = reader.ReadSingle();
            ReceiveTiming(stateId, timer, counter, npc, localStateId, localTimer);
            packetSway = sway;
            swayPending = true;
        }

        /// <summary>计时走同步槽（<c>NPCOverride.ai</c>）而非自带流时的入口</summary>
        public void ReceiveTiming(int stateId, int timer, int counter, NPC npc, int localStateId, int localTimer) {
            packetStateId = stateId;
            packetTimer = timer;
            packetCounter = counter;
            timingPending = true;

            //只有两端处在同一状态时，计时差才是帧相位差；换态包无从比较，按 0 帧处理
            int frameDelta = 0;
            if (stateId == localStateId) {
                int d = localTimer - timer;
                if (Math.Abs(d) <= MaxFrameDelta) {
                    frameDelta = d;
                }
            }
            OnSnapshot(npc, frameDelta);
        }

        /// <summary>
        /// 客户端：取出待收养的计时，状态对得上才给（读后即清）。<br/>
        /// 调用点有两处：AI 开头（同态收包）与状态机换态之后（换态包携带的新态计时）
        /// </summary>
        public bool TryTakeTiming(int localStateId, out int timer, out int counter) {
            timer = 0;
            counter = 0;
            if (!timingPending || packetStateId != localStateId) {
                return false;
            }
            timingPending = false;
            timer = packetTimer;
            counter = packetCounter;
            return true;
        }

        /// <summary>
        /// 客户端：取出快照里的摆动相位（读后即清）。<br/>
        /// 相位是<b>持久累加量</b>，不像 Timer 那样每帧由状态重新声明，两端一旦分家就再也回不来：
        /// 它扰动的是航向，位置误差是相位误差的二重积分，会持续张开而不是抖一下。
        /// 收养点必须在消费它的运动函数之前（AI 开头）
        /// </summary>
        public bool TakeSway(out float sway) {
            sway = packetSway;
            bool had = swayPending;
            swayPending = false;
            return had;
        }

        /// <summary>只差一两帧是网络抖动的常态，硬对齐会让 Timer == X 型一次性拍被跳过或重放</summary>
        public static int AdoptTimer(int local, int synced, int tolerance = TimerTolerance) {
            return Math.Abs(synced - local) > tolerance ? synced : local;
        }

        /// <summary>
        /// 位置由确定性重算的部件（跟链体节、编队仆从、贴锚点的部位）：快照只该纠正数据，
        /// 不该在贴图上留下偏移。原版平滑对它们纯属噪声，逐帧清掉
        /// </summary>
        public static void ClearSmoothing(NPC npc) {
            npc.netOffset = Vector2.Zero;
        }

        /// <summary>
        /// 蓄势颤抖等抖动只走绘制层：原版把 NPC 画在 <c>position + netOffset</c>，
        /// 而本件每帧开头已把 netOffset 清零，所以 AI 期写进去的量只影响本帧贴图，位置与判定分毫不动。<br/>
        /// <b>不要写 <c>npc.position +=</c></b>：那是各端各滚的随机游走，写在 <c>!dedServ</c> 里更糟——
        /// 只有客户端在飘，而且恰好飘在纠偏器要对账的预警帧上
        /// </summary>
        public static void DrawShake(NPC npc, Vector2 offset) {
            npc.netOffset = offset;
        }
    }
}
