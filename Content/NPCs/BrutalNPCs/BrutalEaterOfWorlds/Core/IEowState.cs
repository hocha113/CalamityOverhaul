using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core
{
    /// <summary>状态索引，写入头部 npc.ai[2] 同步</summary>
    internal enum EowStateIndex : int
    {
        /// <summary>入场演出：蚀土之兆→破土贯穿→高空回身</summary>
        Intro = 0,
        /// <summary>连接态：绕体蛇行并选招</summary>
        Weave = 1,
        /// <summary>腐蚀唾液连射(头部抛射+体节涟漪齐射)</summary>
        SpitBarrage = 2,
        /// <summary>三段猛扑(压缩蓄势→爆发直扑)</summary>
        LungeFlurry = 3,
        /// <summary>地底伏击(潜行→锁定→垂直喷发)</summary>
        BurrowAmbush = 4,
        /// <summary>地表犁沟(浅层横掠+行进间歇泉链)</summary>
        GeyserRake = 5,
        /// <summary>节段自爆链(沿途蜕壳布雷→顺序殉爆)</summary>
        HuskMines = 6,
        /// <summary>分裂钳猎(三分→钳形夹击→地底合体喷发)</summary>
        SplitPincer = 7,
        /// <summary>酸雨播撒(高空掠过身上喷吐酸雨帘)</summary>
        AcidRain = 8,
        /// <summary>蜕皮转阶段演出</summary>
        Molt = 9,
        /// <summary>低血大招：群猎终章(四分裂地底轮番喷发→合体巨喷)</summary>
        ApexFrenzy = 10,
        /// <summary>无目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出：尾→头连锁溃爆</summary>
        Death = 12,
        /// <summary>投技·生吞入腹(地底伏击→垂直破土吞人→拖入地底挤压→破土喷出)</summary>
        Devour = 13,
    }

    /// <summary>状态接口</summary>
    internal interface IEowState : IVaultState<EowStateContext>
    {
        EowStateIndex StateIndex { get; }
        void OnEnter(EowStateContext context);
        IEowState OnUpdate(EowStateContext context);
        void OnExit(EowStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class EowStateBase : VaultState<EowStateContext>, IEowState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract EowStateIndex StateIndex { get; }

        /// <summary>远距回归阀允许介入；自带地下走位/分裂的状态应关</summary>
        public virtual bool AllowFarSnap => true;

        public virtual void OnEnter(EowStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IEowState OnUpdate(EowStateContext context);

        public virtual void OnExit(EowStateContext context) {
            context.PulseKind = 0;
            context.PulsePhase = 0f;
            context.MawGlow = 0f;
            context.Compression = 1f;
        }

        public override void OnEnter(VaultStateMachine<EowStateContext> machine, EowStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<EowStateContext> OnUpdate(VaultStateMachine<EowStateContext> machine, EowStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<EowStateContext> machine, EowStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具
        /// <summary>声明头部寻的运动</summary>
        protected void SetMovement(EowStateContext context, Vector2 targetPos, float speed, float turnSpeed) {
            context.TargetPosition = targetPos;
            context.MoveSpeed = speed;
            context.TurnSpeed = turnSpeed;
        }

        /// <summary>到目标方向</summary>
        protected Vector2 DirectionToTarget(EowStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>本状态计时推进，返回推进后的值</summary>
        protected int Tick() => ++Timer;

        /// <summary>把 Timer 直接快进到某拍(消灭干等)</summary>
        protected void JumpTo(int frame) {
            if (Timer < frame) {
                Timer = frame;
            }
        }
        #endregion
    }
}
