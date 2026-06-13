using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>头部状态索引，写入 <c>npc.ai[2]</c> 网络同步</summary>
    internal enum PrimeStateIndex : int
    {
        /// <summary>登场演出：自深渊升起、注能回血、再生四肢</summary>
        Intro = 0,
        /// <summary>指挥序列：武装阶段 hub，短悬停后按固定表分发指令/招式</summary>
        CommandSequence = 1,
        /// <summary>旋转冲撞：武装阶段连段突进</summary>
        SpinDash = 2,
        /// <summary>火力阵：四臂收拢波浪齐射</summary>
        BarrageCommand = 3,
        /// <summary>电弧链锁：四臂飞散十字旋转收紧</summary>
        TetherSpin = 4,
        /// <summary>转阶段：四肢依次殉爆、机体过载重启</summary>
        PhaseTransition = 5,
        /// <summary>狂暴 connector：换弹/排气段落标点</summary>
        RageConnector = 6,
        /// <summary>狂暴冲撞：闪现贯穿三连</summary>
        RageDash = 7,
        /// <summary>离子过载：充能后三波带缺口全向弹环</summary>
        IonOverload = 8,
        /// <summary>火箭帷幕：两侧火箭墙向中线折叠</summary>
        RocketCurtain = 9,
        /// <summary>白昼狂暴</summary>
        DayEnrage = 10,
        /// <summary>金币枪狂怒</summary>
        CoinGunFury = 11,
        /// <summary>脱战离场</summary>
        Despawn = 12,
        /// <summary>死亡演出</summary>
        Death = 13,
        /// <summary>断头台旋杀：大半径圆周锯刃收紧</summary>
        GuillotineSpin = 14,
        /// <summary>颅骨主炮：二阶段固定杀招，巨型光束横扫大半圈</summary>
        SkullCannon = 15,
        /// <summary>十字绞杀：四臂合体对角封位</summary>
        CrossExecute = 16,
        /// <summary>战术指令执行窗口（广播四臂后衔接下一招）</summary>
        CommandExecute = 17,
    }

    /// <summary>头部状态接口</summary>
    internal interface IPrimeState : IVaultState<PrimeStateContext>
    {
        PrimeStateIndex StateIndex { get; }
        void OnEnter(PrimeStateContext context);
        IPrimeState OnUpdate(PrimeStateContext context);
        void OnExit(PrimeStateContext context);
    }

    /// <summary>头部状态基类</summary>
    internal abstract class PrimeStateBase : VaultState<PrimeStateContext>, IPrimeState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract PrimeStateIndex StateIndex { get; }

        public virtual void OnEnter(PrimeStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IPrimeState OnUpdate(PrimeStateContext context);

        public virtual void OnExit(PrimeStateContext context) {
            context.ResetChargeState();
        }

        public sealed override void OnEnter(VaultStateMachine<PrimeStateContext> machine, PrimeStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<PrimeStateContext> OnUpdate(VaultStateMachine<PrimeStateContext> machine, PrimeStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<PrimeStateContext> machine, PrimeStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>分轴悬停，垂直 vOffset~vThreshold，水平 ±100</summary>
        protected static void HoverMovement(PrimeStateContext ctx, float vAccel, float vMax,
            float hAccel, float hMax, float decel, int vOffset, int vThreshold) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;

            if (npc.position.Y > target.position.Y - vOffset) {
                if (npc.velocity.Y > 0f) {
                    npc.velocity.Y *= decel;
                }
                npc.velocity.Y -= vAccel;
                if (npc.velocity.Y > vMax) {
                    npc.velocity.Y = vMax;
                }
            }
            else if (npc.position.Y < target.position.Y - vThreshold) {
                if (npc.velocity.Y < 0f) {
                    npc.velocity.Y *= decel;
                }
                npc.velocity.Y += vAccel;
                if (npc.velocity.Y < -vMax) {
                    npc.velocity.Y = -vMax;
                }
            }

            if (npc.Center.X > target.Center.X + 100f) {
                if (npc.velocity.X > 0f) {
                    npc.velocity.X *= decel;
                }
                npc.velocity.X -= hAccel;
                if (npc.velocity.X > hMax) {
                    npc.velocity.X = hMax;
                }
            }
            if (npc.Center.X < target.Center.X - 100f) {
                if (npc.velocity.X < 0f) {
                    npc.velocity.X *= decel;
                }
                npc.velocity.X += hAccel;
                if (npc.velocity.X < -hMax) {
                    npc.velocity.X = -hMax;
                }
            }
        }

        /// <summary>随水平速度轻微倾头（悬停常态姿势）</summary>
        protected static void LeanByVelocity(NPC npc) {
            npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
        }

        /// <summary>朝向目标方向倾头（登场/转阶段定点姿势）</summary>
        protected static void LeanTowards(NPC npc, Vector2 targetCenter) {
            Vector2 toTarget = npc.Center.To(targetCenter);
            npc.rotation = npc.rotation.AngleLerp(toTarget.X / 115f * 0.5f, 0.75f);
        }

        /// <summary>冲撞期的持续旋转</summary>
        protected static void SpinRotation(NPC npc, float speed = 0.3f) {
            npc.rotation += npc.direction * speed;
        }

        protected static Vector2 DirectionToTarget(PrimeStateContext ctx) {
            return (ctx.Target.Center - ctx.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>套用全局难度伤害修正</summary>
        protected static int ScaleDamage(int damage) => HeadPrimeAI.SetMultiplier(damage);

        #endregion
    }
}
