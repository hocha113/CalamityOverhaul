using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>头部状态索引，写入 npc.ai[2] 网络同步</summary>
    internal enum SkeletronStateIndex : int
    {
        /// <summary>诅咒仪式登场</summary>
        Intro = 0,
        /// <summary>悬浮压制连接件</summary>
        Hub = 1,
        /// <summary>锁链砸击连段</summary>
        HandCrush = 2,
        /// <summary>双掌合拍钳杀</summary>
        ClapPincer = 3,
        /// <summary>幽灵臂环猎</summary>
        GhostArmCircle = 4,
        /// <summary>旋杀骨风暴</summary>
        SpinBoneStorm = 5,
        /// <summary>断手狂化转阶段</summary>
        PhaseTransition = 6,
        /// <summary>瞬猎颅雨</summary>
        SkullRainTeleport = 7,
        /// <summary>群臂万象</summary>
        GhostPandemonium = 8,
        /// <summary>诅咒黑暗领域</summary>
        CurseDomain = 9,
        /// <summary>万骨临渊（低血大招）</summary>
        BoneMaelstrom = 10,
        /// <summary>白昼狂暴</summary>
        DayEnrage = 11,
        /// <summary>脱战消散</summary>
        Despawn = 12,
        /// <summary>诅咒崩解死亡演出</summary>
        Death = 13,
        /// <summary>合掌拍捉（投技）</summary>
        PalmSnatch = 14,
        /// <summary>旋骨罗盘（二阶段签名：旋骨轮钳杀）</summary>
        BoneWheel = 15,
    }

    /// <summary>头部状态接口</summary>
    internal interface ISkeletronState : IVaultState<SkeletronStateContext>
    {
        SkeletronStateIndex StateIndex { get; }
        void OnEnter(SkeletronStateContext context);
        ISkeletronState OnUpdate(SkeletronStateContext context);
        void OnExit(SkeletronStateContext context);
    }

    /// <summary>头部状态基类</summary>
    internal abstract class SkeletronStateBase : VaultState<SkeletronStateContext>, ISkeletronState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract SkeletronStateIndex StateIndex { get; }

        public virtual void OnEnter(SkeletronStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ISkeletronState OnUpdate(SkeletronStateContext context);

        public virtual void OnExit(SkeletronStateContext context) {
            context.ResetTransientVisuals();
        }

        public sealed override void OnEnter(VaultStateMachine<SkeletronStateContext> machine, SkeletronStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<SkeletronStateContext> OnUpdate(VaultStateMachine<SkeletronStateContext> machine, SkeletronStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<SkeletronStateContext> machine, SkeletronStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>分轴悬停（骷髅王式浮游）</summary>
        protected static void HoverMovement(SkeletronStateContext ctx, float vAccel, float vMax,
            float hAccel, float hMax, float decel, int vOffset, float xDeadZone = 100f) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;

            if (npc.Top.Y > target.Top.Y - vOffset) {
                if (npc.velocity.Y > 0f) {
                    npc.velocity.Y *= decel;
                }
                npc.velocity.Y -= vAccel;
                if (npc.velocity.Y < -vMax) {
                    npc.velocity.Y = -vMax;
                }
            }
            else if (npc.Top.Y < target.Top.Y - vOffset) {
                if (npc.velocity.Y < 0f) {
                    npc.velocity.Y *= decel;
                }
                npc.velocity.Y += vAccel;
                if (npc.velocity.Y > vMax) {
                    npc.velocity.Y = vMax;
                }
            }

            if (npc.Center.X > target.Center.X + xDeadZone) {
                if (npc.velocity.X > 0f) {
                    npc.velocity.X *= decel;
                }
                npc.velocity.X -= hAccel;
                if (npc.velocity.X < -hMax) {
                    npc.velocity.X = -hMax;
                }
            }
            if (npc.Center.X < target.Center.X - xDeadZone) {
                if (npc.velocity.X < 0f) {
                    npc.velocity.X *= decel;
                }
                npc.velocity.X += hAccel;
                if (npc.velocity.X > hMax) {
                    npc.velocity.X = hMax;
                }
            }
        }

        /// <summary>悬停倾头</summary>
        protected static void LeanByVelocity(NPC npc) {
            npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.4f);
        }

        /// <summary>旋转（旋杀期间）</summary>
        protected static void SpinRotation(NPC npc, float speed = 0.32f) {
            npc.rotation += (npc.direction == 0 ? 1 : npc.direction) * speed;
        }

        /// <summary>回正旋转</summary>
        protected static void SettleRotation(NPC npc, float rate = 0.12f) {
            npc.rotation = npc.rotation.AngleLerp(0f, rate);
        }

        protected static Vector2 DirectionToTarget(SkeletronStateContext ctx) {
            return (ctx.Target.Center - ctx.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>难度伤害修正</summary>
        protected static int ScaleDamage(int damage) => SkeletronHeadAI.SetMultiplier(damage);

        /// <summary>骷髅弹幕基准伤害</summary>
        protected static int SkullDamage(SkeletronStateContext ctx) => SkeletronHeadAI.GetSkullDamage(ctx.Npc);

        #endregion
    }
}
