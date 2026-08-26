using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 状态索引,写入 npc.ai[2] 同步<br/>
    /// 星轨司祭:巨型天体是主星,浑天仪是法器;四式+掷星全程可见,月瞳凝视是月明专属;<br/>
    /// 合相充能满格触发合相祭仪(阶段大招),蓄力窗内重创可打断
    /// </summary>
    internal enum CultistStateIndex : int
    {
        /// <summary>入场:三环逐一显形→黄道环定界→首星降临</summary>
        Intro = 0,
        /// <summary>盘转连接段:悬浮压场+选招,星球轮替出手的窗口</summary>
        Coil = 1,
        /// <summary>星轨连珠:倾斜轨道椭圆+星珠巡行,只在近平面咬人</summary>
        OrbitLance = 2,
        /// <summary>掷环:三环离体锁向掷出,回旋归位</summary>
        RingHurl = 3,
        /// <summary>星图审判:星座连线描绘→定形→沿边线放光矛</summary>
        StarChart = 4,
        /// <summary>蚀祭:暗影盘掩主星,全食冕矛辐射,本影楔是唯一安全走廊</summary>
        Eclipse = 5,
        /// <summary>合相祭仪:充能满格的阶段大招,蓄力窗可被打断</summary>
        Conjunction = 6,
        /// <summary>掷星:主星收势远平面→锁向掷出→自行归位</summary>
        PlanetHurl = 7,
        /// <summary>月瞳凝视:月面竖瞳睁开,扫射凝视光束(月明专属)</summary>
        Gaze = 8,
        /// <summary>失衡:合相被打断的硬直,受伤加深</summary>
        Stagger = 9,
        /// <summary>转阶段:旧星裂解→浑天仪调律→新星降临</summary>
        PhaseShift = 10,
        /// <summary>死亡演出:三环逐一崩碎+主星裂解内爆</summary>
        Death = 11,
        /// <summary>无目标撤离</summary>
        Despawn = 12,
    }

    /// <summary>状态接口</summary>
    internal interface ICultistState : IVaultState<CultistStateContext>
    {
        CultistStateIndex StateIndex { get; }
        void OnEnter(CultistStateContext context);
        /// <returns>下一态,null=保持</returns>
        ICultistState OnUpdate(CultistStateContext context);
        void OnExit(CultistStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class CultistStateBase : VaultState<CultistStateContext>, ICultistState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract CultistStateIndex StateIndex { get; }

        public virtual void OnEnter(CultistStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ICultistState OnUpdate(CultistStateContext context);

        public virtual void OnExit(CultistStateContext context) {
        }

        public override void OnEnter(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<CultistStateContext> OnUpdate(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>vanilla 帧语法:localAI[2] 切动画档并复位帧计数(0 悬浮 11 举手 12 施法 13 祈祷)</summary>
        protected static void SetPose(NPC npc, int poseCode) {
            if (npc.localAI[2] != poseCode) {
                npc.localAI[2] = poseCode;
                npc.frameCounter = 0;
            }
        }

        protected static Vector2 DirectionToTarget(CultistStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>面向目标(本体贴图只有左右向)</summary>
        protected static void FaceTarget(NPC npc, Vector2 targetCenter) {
            npc.direction = npc.spriteDirection = targetCenter.X >= npc.Center.X ? 1 : -1;
        }

        #endregion
    }
}
