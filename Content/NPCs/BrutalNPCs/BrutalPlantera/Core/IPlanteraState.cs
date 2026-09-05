using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum PlanteraStateIndex : int
    {
        /// <summary>入场演出，钩爪破土拽出花苞→绽放</summary>
        Intro = 0,
        /// <summary>悬吊巡航连接态，选下一招</summary>
        Canopy = 1,
        /// <summary>种子加特林弹幕压制</summary>
        SeedGatling = 2,
        /// <summary>钩爪锚定+藤蔓弹弓猛扑</summary>
        GrapplePounce = 3,
        /// <summary>藤蔓格栅重塑战场</summary>
        VineLattice = 4,
        /// <summary>孢子云播撒，漂浮地雷生态</summary>
        SporeSow = 5,
        /// <summary>一阶段→二阶段蜕壳演出</summary>
        PhaseTransition = 6,
        /// <summary>二阶段连环狂扑</summary>
        FrenzyPounce = 7,
        /// <summary>二阶段触手绽放处刑圈</summary>
        TentacleRing = 8,
        /// <summary>二阶段触手鞭刑连段</summary>
        WhipBarrage = 9,
        /// <summary>低血大招，凋零绽放新星</summary>
        BloomNova = 10,
        /// <summary>无目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出，钩爪逐根断裂→坠落</summary>
        Death = 12,
        /// <summary>二阶段投技：缠足藤索拖回巨口咀嚼，吐飞收尾</summary>
        VineFeast = 13,
    }

    /// <summary>状态接口</summary>
    internal interface IPlanteraState : IVaultState<PlanteraStateContext>
    {
        PlanteraStateIndex StateIndex { get; }
        void OnEnter(PlanteraStateContext context);
        IPlanteraState OnUpdate(PlanteraStateContext context);
        void OnExit(PlanteraStateContext context);
    }

    /// <summary>
    /// 热字段同步槽：主控 <c>PlanteraAI.ai[]</c>(InnoVault 重制节点自带 12 槽，随原版 SyncNPC 的
    /// ExtraAI 与位置/速度/npc.ai 同包原子到达)。运动在各端确定性积分，这些槽只负责把
    /// 服务端的计时器与一次性决策带给客户端，让两端的状态数学从同一起点推进
    /// </summary>
    internal static class PlanteraHotSlot
    {
        /// <summary>悬吊摆动相位</summary>
        public const int Sway = 0;
        /// <summary>位标志：bit0 激怒</summary>
        public const int Flags = 1;
        /// <summary>状态 Timer</summary>
        public const int Timer = 2;
        /// <summary>状态 Counter</summary>
        public const int Counter = 3;
        /// <summary>状态自用槽 A~F</summary>
        public const int A = 4;
        public const int B = 5;
        public const int C = 6;
        public const int D = 7;
        public const int E = 8;
        public const int F = 9;

        public const int FlagEnraged = 1;
    }

    /// <summary>状态基类</summary>
    internal abstract class PlanteraStateBase : VaultState<PlanteraStateContext>, IPlanteraState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract PlanteraStateIndex StateIndex { get; }

        /// <summary>
        /// 一次性演出拍的追帧宽限：收养的 Timer 刚越过拍点不超过这么多帧，视为本机只是慢了半拍，
        /// 让本地一次性逻辑照常触发；越过更多则是中途加入，静默跳过不补放
        /// </summary>
        protected const int CueCatchUpGrace = 20;

        public virtual void OnEnter(PlanteraStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IPlanteraState OnUpdate(PlanteraStateContext context);

        public virtual void OnExit(PlanteraStateContext context) {
            context.ResetChargeState();
        }

        /// <summary>权威端每帧末把热字段写进同步槽；子类先调 base 再写自用槽</summary>
        public virtual void WriteHot(float[] hot, PlanteraStateContext context) {
            hot[PlanteraHotSlot.Timer] = Timer;
            hot[PlanteraHotSlot.Counter] = Counter;
        }

        /// <summary>
        /// 客户端收包后从同步槽恢复热字段；子类先调 base 再读自用槽，派生的一次性标志在此重算。
        /// 可能在 SetDefaults 期被调(初始态)，此时 <c>context.Target</c> 为 null，实现里不要碰目标
        /// </summary>
        public virtual void ReadHot(float[] hot, PlanteraStateContext context) {
            Timer = AdoptTimer(Timer, hot[PlanteraHotSlot.Timer]);
            Counter = (int)hot[PlanteraHotSlot.Counter];
        }

        /// <summary>
        /// 计时器收养带容差：本地时钟与服务端只差一两帧是网络抖动的常态，硬对齐会让
        /// <c>Timer == X</c> 型的一次性拍被跳过或重放；只在真正漂开时才拉齐
        /// </summary>
        protected static int AdoptTimer(int local, float synced, int tolerance = 2) {
            int server = (int)synced;
            return System.Math.Abs(server - local) > tolerance ? server : local;
        }

        public override void OnEnter(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<PlanteraStateContext> OnUpdate(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>悬吊移动参数，主控 UpdateSuspension 消费</summary>
        protected static void SetSuspension(PlanteraStateContext context, Vector2 anchorOffset, float speed, float accel) {
            context.SuspendOffset = anchorOffset;
            context.MoveSpeed = speed;
            context.AccelRate = accel;
        }

        /// <summary>朝目标平滑转体</summary>
        protected static void FaceTarget(NPC npc, Vector2 target, float lerpFactor = 0.14f) {
            float targetAngle = (target - npc.Center).ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, lerpFactor);
        }

        /// <summary>到玩家单位方向</summary>
        protected static Vector2 DirectionToTarget(PlanteraStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
