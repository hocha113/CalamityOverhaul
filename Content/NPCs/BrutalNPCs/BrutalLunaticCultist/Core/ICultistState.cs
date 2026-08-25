using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步。五技能=火焰/落星/闪电/幻象/月明激光,各在主场阶段强化</summary>
    internal enum CultistStateIndex : int
    {
        /// <summary>入场：法阵描绘+限制圈定形+首颗星球降临</summary>
        Intro = 0,
        /// <summary>悬浮压场连接段，慢弹点射+选招</summary>
        Weave = 1,
        /// <summary>帷幕挪移：符文散身+出口印记+重现</summary>
        VeilStep = 2,
        /// <summary>火焰：印记狩猎喷焰扇+燃地，日耀主场强化</summary>
        FlameRite = 3,
        /// <summary>落星：声明角度的坠星雨，星尘主场强化</summary>
        StarRite = 4,
        /// <summary>闪电：三拍雷律细弧预告落雷，星旋主场强化</summary>
        BoltRite = 5,
        /// <summary>幻象：真假弹幕扇，遮挡即识真线索，星云主场强化</summary>
        PhantomRite = 6,
        /// <summary>仪式咏唱：法阵快充，环轨法球护体，可被打断</summary>
        Chant = 7,
        /// <summary>镜像仪式：真假身环阵，读线索点真身</summary>
        MirrorRite = 8,
        /// <summary>仪式迸发：充能满格的阶段大招</summary>
        RiteBurst = 9,
        /// <summary>转阶段演出：旧星球退场+新星球降临</summary>
        PhaseShift = 10,
        /// <summary>仪式被破的踉跄硬直，受伤加深</summary>
        Stagger = 11,
        /// <summary>死亡演出：法阵崩解</summary>
        Death = 12,
        /// <summary>无目标撤离</summary>
        Despawn = 13,
        /// <summary>月明激光：月亮睁眼放辐条死光，本体跪祷不出手</summary>
        MoonLaser = 14,
    }

    /// <summary>状态接口</summary>
    internal interface ICultistState : IVaultState<CultistStateContext>
    {
        CultistStateIndex StateIndex { get; }
        void OnEnter(CultistStateContext context);
        /// <returns>下一态，null=保持</returns>
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

        /// <summary>vanilla 帧语法：localAI[2] 切动画档并复位帧计数</summary>
        protected static void SetPose(NPC npc, int poseCode) {
            if (npc.localAI[2] != poseCode) {
                npc.localAI[2] = poseCode;
                npc.frameCounter = 0;
            }
        }

        protected static Vector2 DirectionToTarget(CultistStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>面向目标（本体贴图只有左右向）</summary>
        protected static void FaceTarget(NPC npc, Vector2 targetCenter) {
            npc.direction = npc.spriteDirection = targetCenter.X >= npc.Center.X ? 1 : -1;
        }

        #endregion
    }
}
