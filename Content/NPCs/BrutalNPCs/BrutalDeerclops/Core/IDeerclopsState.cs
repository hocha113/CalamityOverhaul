using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum DeerclopsStateIndex : int
    {
        Intro = 0,
        /// <summary>连接态，走位逼近+选招</summary>
        Stalk = 1,
        /// <summary>冰刺波列，前向多波错拍</summary>
        SpikeWave = 2,
        /// <summary>双侧合拢牢笼+中心延迟爆发</summary>
        SpikeCage = 3,
        /// <summary>跺脚震荡，贴地行进霜脉冲</summary>
        FrostQuake = 4,
        /// <summary>掀地投掷，冰岩弹幕抛射</summary>
        RubbleToss = 5,
        /// <summary>屏幕边缘暗影之手，逐玩家结算</summary>
        ShadowClaw = 6,
        /// <summary>凝视吼叫，与它对视者受罚</summary>
        GazeRoar = 7,
        /// <summary>低头冲撞，尾迹冰刺</summary>
        AvalancheCharge = 8,
        /// <summary>阶段转换演出，暴风雪升级</summary>
        PhaseRoar = 9,
        /// <summary>低血大招，白澈领域反转安全区</summary>
        Whiteout = 10,
        Despawn = 11,
        Death = 12,
        /// <summary>投技·攫取：垂首聚影放出攫取手，命中则转入携抓</summary>
        SeizeHunt = 13,
        /// <summary>投技·凝视擒抱：拖回→拎至独眼→凝视→吐息→砸雪释放</summary>
        EyeGrab = 14,
    }

    /// <summary>帧动画命令，FindFrame 接管消费</summary>
    internal enum DeerAnimMode : int
    {
        /// <summary>自动行走/站立/跳跃</summary>
        Locomotion = 0,
        /// <summary>跺脚攻击帧序(12-17)，AnimTimer 驱动</summary>
        Stomp = 1,
        /// <summary>掀地帧序(12,15-18)，AnimTimer 驱动</summary>
        Scoop = 2,
        /// <summary>吼叫帧序(19-24)，AnimTimer 驱动</summary>
        Roar = 3,
        /// <summary>踉跄/伏低固定帧</summary>
        Crouch = 4,
    }

    /// <summary>状态接口</summary>
    internal interface IDeerclopsState : IVaultState<DeerclopsStateContext>
    {
        DeerclopsStateIndex StateIndex { get; }
        void OnEnter(DeerclopsStateContext context);
        IDeerclopsState OnUpdate(DeerclopsStateContext context);
        void OnExit(DeerclopsStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class DeerclopsStateBase : VaultState<DeerclopsStateContext>, IDeerclopsState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract DeerclopsStateIndex StateIndex { get; }

        /// <summary>卡地形阀是否允许暴风雪瞬步，仅潜行连接态放行</summary>
        public virtual bool AllowBlizzardStep => false;

        public virtual void OnEnter(DeerclopsStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IDeerclopsState OnUpdate(DeerclopsStateContext context);

        public virtual void OnExit(DeerclopsStateContext context) {
            context.ResetPerStateCommands();
        }

        public override void OnEnter(VaultStateMachine<DeerclopsStateContext> machine, DeerclopsStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<DeerclopsStateContext> OnUpdate(VaultStateMachine<DeerclopsStateContext> machine, DeerclopsStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<DeerclopsStateContext> machine, DeerclopsStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>面向目标(仅设 direction，绘制读取)</summary>
        protected static void FaceTarget(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            if (context.Target == null) {
                return;
            }
            float dx = context.Target.Center.X - npc.Center.X;
            if (System.Math.Abs(dx) > 24f) {
                npc.direction = npc.spriteDirection = System.Math.Sign(dx);
            }
        }

        /// <summary>目标相对boss的水平方向</summary>
        protected static int DirToTarget(DeerclopsStateContext context) {
            if (context.Target == null) {
                return context.Npc.direction != 0 ? context.Npc.direction : 1;
            }
            int sign = System.Math.Sign(context.Target.Center.X - context.Npc.Center.X);
            return sign == 0 ? 1 : sign;
        }

        /// <summary>预兆时长按死亡模式压缩，留可读下限</summary>
        protected static int TelegraphTime(DeerclopsStateContext context, int baseFrames, int floorFrames) {
            if (!context.IsDeathMode) {
                return baseFrames;
            }
            int scaled = (int)(baseFrames * 0.85f);
            return System.Math.Max(scaled, floorFrames);
        }

        #endregion
    }
}
