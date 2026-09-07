using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum EocStateIndex : int
    {
        /// <summary>入场演出，血雾凝聚成眼</summary>
        Intro = 0,
        /// <summary>悬停压场连接段，血弹点射+选招</summary>
        VeilHover = 1,
        /// <summary>变轨假动作冲刺，中途拐折+谎言残影</summary>
        FeintDash = 2,
        /// <summary>血雾播场+雾中伏击</summary>
        FogAmbush = 3,
        /// <summary>仆从血枪列，纵队逐发</summary>
        ServantLance = 4,
        /// <summary>仆从血环合围，二阶段</summary>
        ServantEncircle = 5,
        /// <summary>溢血喷泉，旋喷重力血弹</summary>
        BloodFountain = 6,
        /// <summary>撕皮转阶段演出</summary>
        PhaseTransition = 7,
        /// <summary>口器狂化锯齿撕咬连冲，二阶段</summary>
        MawFrenzy = 8,
        /// <summary>盲侧横贯，假高位坠压→横线暴冲，二阶段</summary>
        BlindsideCross = 9,
        /// <summary>猩红血漩涡，低血大招</summary>
        Maelstrom = 10,
        /// <summary>无目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出</summary>
        Death = 12,
        /// <summary>撕咬拖曳投技，二阶段</summary>
        MawDrag = 13,
    }

    /// <summary>状态接口</summary>
    internal interface IEocState : IVaultState<EocStateContext>
    {
        EocStateIndex StateIndex { get; }
        void OnEnter(EocStateContext context);
        /// <returns>下一态，null=保持</returns>
        IEocState OnUpdate(EocStateContext context);
        void OnExit(EocStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class EocStateBase : VaultState<EocStateContext>, IEocState, IBossNetTiming
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract EocStateIndex StateIndex { get; }

        /// <summary>远距雾步回归阀，演出/伏击/大招关</summary>
        public virtual bool AllowFogStep => true;

        public virtual void OnEnter(EocStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IEocState OnUpdate(EocStateContext context);

        public virtual void OnExit(EocStateContext context) {
            context.ResetChargeState();
            context.LaneIntensity = 0f;
            context.LaneLocked = false;
            context.ClearLaneExtra();
        }

        public override void OnEnter(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<EocStateContext> OnUpdate(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            OnExit(ctx);
        }

        /// <summary>
        /// 收养权威端随快照过线的状态计时（客户端）。容差内不动本地值：
        /// 只差一两帧是网络抖动的常态，硬对齐会让 Timer == X 型一次性拍被跳过或重放
        /// </summary>
        public void AdoptNetTiming(int timer, int counter) {
            Timer = BossNetMotion.AdoptTimer(Timer, timer);
            Counter = counter;
        }

        #region 工具方法

        /// <summary>瞳孔指向目标，rotation=朝向-PiOver2</summary>
        protected static void FaceTarget(NPC npc, Vector2 targetCenter, float lerpFactor = 1f) {
            float targetRot = (targetCenter - npc.Center).ToRotation() - MathHelper.PiOver2;
            npc.rotation = lerpFactor >= 1f ? targetRot : npc.rotation.AngleLerp(targetRot, lerpFactor);
        }

        /// <summary>朝向速度方向</summary>
        protected static void FaceVelocity(NPC npc) {
            if (npc.velocity.Length() > 0.1f) {
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            }
        }

        protected static Vector2 DirectionToTarget(EocStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>仅高速开接触伤，窗口对齐视觉冲刺</summary>
        protected static void EnableContactDamageIfFast(NPC npc, float minSpeed = 24f, float mult = 1f) {
            npc.damage = npc.velocity.Length() >= minSpeed ? (int)(npc.defDamage * mult) : 0;
        }

        protected static void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        /// <summary>
        /// 预告即承诺：前摇最后 lockFrames 帧冻结瞄准，之后车道与起跑共用同一方向，起跑后不再重瞄。<br/>
        /// 未锁定时每帧跟踪 liveDir；到点锁定并返回 true（当帧播锁定音画）
        /// </summary>
        protected static bool UpdateAimLock(ref Vector2 lockedDir, ref bool locked, Vector2 liveDir,
            int timer, int telegraphTime, int lockFrames) {
            if (locked) {
                return false;
            }
            lockedDir = liveDir;
            if (timer >= telegraphTime - lockFrames) {
                locked = true;
                return true;
            }
            return false;
        }

        /// <summary>写主车道：锁定后满亮定格，未锁定随进度渐显</summary>
        protected static void WriteLane(EocStateContext context, Vector2 start, Vector2 dir, float length,
            float progress, bool locked, float baseIntensity = 0.4f) {
            context.LaneStart = start;
            context.LaneDir = dir;
            context.LaneLength = length;
            context.LaneProgress = progress;
            context.LaneLocked = locked;
            context.LaneIntensity = locked ? 1f : baseIntensity + (1f - baseIntensity) * progress;
            context.ClearLaneExtra();
        }

        #endregion
    }
}
