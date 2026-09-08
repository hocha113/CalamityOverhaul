using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum EmpressStateIndex : int
    {
        Intro = 0,
        /// <summary>衔接：贴身追击+攻击选择</summary>
        Connector = 1,
        /// <summary>光球螺旋：就位→冲击波推开→八臂加速光球</summary>
        LightSpiral = 2,
        /// <summary>冲刺抓取：预测转向追击，命中且冷却到则接投技</summary>
        DashGrab = 3,
        /// <summary>日舞：按偏角表逐发追踪光束</summary>
        SunDance = 4,
        /// <summary>长枪墙：出生点偏移+跳索引缝隙+衰减追踪</summary>
        LanceWall = 5,
        /// <summary>万华镜：第一拍标落点、下一小节第一拍整条鞭线抽下，强弱弱三连</summary>
        Kaleidoscope = 6,
        /// <summary>光痕：每拍留下玩家残像，两小节后凝成琉璃剑，一小节后碎</summary>
        Echo = 7,
        /// <summary>蝶群：光蝶飘向玩家的平均位置，合掌时冻成琉璃</summary>
        Lacewing = 8,
        /// <summary>月影（昼）：世界照白，唯一安全处是月屑投下的阴影锥</summary>
        MoonShadow = 9,
        /// <summary>半血变身</summary>
        PhaseTransition = 10,
        Despawn = 12,
        /// <summary>死亡演出，光之消散+认输独白</summary>
        Death = 13,
        /// <summary>光绫缚舞投技</summary>
        LightBindWaltz = 14,
        /// <summary>三阶段变身：台词+回血+相位姿势</summary>
        Ascension = 15,
        /// <summary>终章步进剧本：加速墙→智能枪→反向日舞→缩圈</summary>
        Finale = 16,
    }

    /// <summary>状态接口</summary>
    internal interface IEmpressState : IVaultState<EmpressStateContext>
    {
        EmpressStateIndex StateIndex { get; }
        void OnEnter(EmpressStateContext context);
        IEmpressState OnUpdate(EmpressStateContext context);
        void OnExit(EmpressStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class EmpressStateBase : VaultState<EmpressStateContext>, IEmpressState, IBossNetTiming
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract EmpressStateIndex StateIndex { get; }

        public virtual void OnEnter(EmpressStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IEmpressState OnUpdate(EmpressStateContext context);

        public virtual void OnExit(EmpressStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<EmpressStateContext> OnUpdate(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
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

        /// <summary>阻尼弹簧滑翔悬停</summary>
        protected static void GlideTo(NPC npc, Vector2 target, float stiffness = 0.016f, float damping = 0.085f, float maxSpeed = 26f) {
            EmpressMotion.SpringGlide(npc, target, stiffness, damping, maxSpeed);
        }

        /// <summary>到玩家方向</summary>
        protected static Vector2 DirectionToTarget(EmpressStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>本端播放音效（服务器空放安全，仍显式挡掉）</summary>
        protected static void PlayLocal(Terraria.Audio.SoundStyle style, Vector2 pos) {
            if (!VaultUtils.isServer) {
                Terraria.Audio.SoundEngine.PlaySound(style, pos);
            }
        }

        /// <summary>停顿末尾一跃入招（停顿不是站着等计时器）</summary>
        protected static void HopOut(NPC npc) {
            npc.velocity.Y = -15f;
            npc.velocity.X *= 0.3f;
        }

        #endregion
    }
}
