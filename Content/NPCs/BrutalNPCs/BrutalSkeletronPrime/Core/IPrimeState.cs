using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>
    /// 机械骷髅王头部状态索引，写入 <c>npc.ai[2]</c> 用于网络同步
    /// </summary>
    internal enum PrimeStateIndex : int
    {
        /// <summary>登场演出：自深渊升起、注能回血、再生四肢</summary>
        Intro = 0,
        /// <summary>指挥悬停：武装阶段常态，头部压阵、四肢输出</summary>
        CommandHover = 1,
        /// <summary>旋转冲撞：武装阶段的连段突进</summary>
        SpinDash = 2,
        /// <summary>机械风暴：召唤毁灭者协奏弹幕领域</summary>
        MechStorm = 3,
        /// <summary>传送恢复：风暴结束传送后的短暂整备</summary>
        TeleportRecover = 4,
        /// <summary>转阶段：四肢依次殉爆、机体过载重启</summary>
        PhaseTransition = 5,
        /// <summary>狂暴悬停：三阶段常态，头颅弹幕压制</summary>
        RageHover = 6,
        /// <summary>狂暴冲撞：三阶段高速连冲</summary>
        RageDash = 7,
        /// <summary>环形爆发：全向弹幕脉冲</summary>
        RadialBurst = 8,
        /// <summary>弹幕墙：自下而上与侧向的火箭洪流</summary>
        LaserWall = 9,
        /// <summary>白昼狂暴</summary>
        DayEnrage = 10,
        /// <summary>金币枪狂怒</summary>
        CoinGunFury = 11,
        /// <summary>脱战离场</summary>
        Despawn = 12,
        /// <summary>死亡演出</summary>
        Death = 13,
    }

    /// <summary>
    /// 机械骷髅王头部状态接口
    /// </summary>
    internal interface IPrimeState : IVaultState<PrimeStateContext>
    {
        PrimeStateIndex StateIndex { get; }
        void OnEnter(PrimeStateContext context);
        IPrimeState OnUpdate(PrimeStateContext context);
        void OnExit(PrimeStateContext context);
    }

    /// <summary>
    /// 机械骷髅王头部状态基类
    /// </summary>
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

        /// <summary>
        /// 经典分轴悬停：垂直方向稳定在玩家上方 [vOffset, vThreshold] 区间，水平方向贴近玩家 ±100
        /// </summary>
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
