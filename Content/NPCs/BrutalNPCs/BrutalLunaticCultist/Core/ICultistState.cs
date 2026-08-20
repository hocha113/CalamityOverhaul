using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum CultistStateIndex : int
    {
        /// <summary>入场：法阵描绘+真身显形</summary>
        Intro = 0,
        /// <summary>悬浮压场连接段，慢弹点射+选招</summary>
        Weave = 1,
        /// <summary>帷幕挪移：符文散身+出口印记+重现</summary>
        VeilStep = 2,
        /// <summary>火：三连印记狩猎，印记定形后喷焰扇</summary>
        FlameHunt = 3,
        /// <summary>冰：印记放射晶枪列，占场拒止</summary>
        FrostLattice = 4,
        /// <summary>雷：三拍雷律，细弧预告后落雷</summary>
        StormCadence = 5,
        /// <summary>古咒唤影：召唤远古厄运/光辉，本体退场露破绽</summary>
        AncientRite = 6,
        /// <summary>仪式咏唱：法阵快充，环轨法球护体，可被打断</summary>
        Chant = 7,
        /// <summary>镜像仪式：真假身环阵，读线索点真身</summary>
        MirrorRite = 8,
        /// <summary>仪式迸发：充能满格的元素大招</summary>
        RiteBurst = 9,
        /// <summary>转阶段演出，P3 唤出幻影龙</summary>
        PhaseShift = 10,
        /// <summary>仪式被破的踉跄硬直，受伤加深</summary>
        Stagger = 11,
        /// <summary>死亡演出：法阵崩解</summary>
        Death = 12,
        /// <summary>无目标撤离</summary>
        Despawn = 13,
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
