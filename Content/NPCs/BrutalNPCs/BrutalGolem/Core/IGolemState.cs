using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>躯干状态索引，写入 npc.ai[2] 网络同步</summary>
    internal enum GolemStateIndex : int
    {
        /// <summary>祭坛启动登场</summary>
        Intro = 0,
        /// <summary>出招衔接 hub</summary>
        Connector = 1,
        /// <summary>重踏跳跃连段</summary>
        StompCombo = 2,
        /// <summary>拳击连段</summary>
        PunchCombo = 3,
        /// <summary>回旋勾拳</summary>
        HookSwing = 4,
        /// <summary>机关乐谱</summary>
        TrapScore = 5,
        /// <summary>太阳宝石弹幕</summary>
        SunBarrage = 6,
        /// <summary>头部分离仪式（阶段转换演出）</summary>
        HeadDetach = 7,
        /// <summary>交叉火力</summary>
        Crossfire = 8,
        /// <summary>陨落重压</summary>
        MeteorLeap = 9,
        /// <summary>太阳核心过载（低血大招）</summary>
        SolarOverdrive = 10,
        /// <summary>脱战离场</summary>
        Despawn = 11,
        /// <summary>石像崩解（死亡演出）</summary>
        Death = 12,
        /// <summary>壁咚研磨投技（二阶段，超级直拳命中触发）</summary>
        WallSlam = 13,
    }

    /// <summary>躯干状态接口</summary>
    internal interface IGolemState : IVaultState<GolemStateContext>
    {
        GolemStateIndex StateIndex { get; }
        void OnEnter(GolemStateContext context);
        IGolemState OnUpdate(GolemStateContext context);
        void OnExit(GolemStateContext context);
    }

    /// <summary>躯干状态基类</summary>
    internal abstract class GolemStateBase : VaultState<GolemStateContext>, IGolemState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract GolemStateIndex StateIndex { get; }

        public virtual void OnEnter(GolemStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IGolemState OnUpdate(GolemStateContext context);

        public virtual void OnExit(GolemStateContext context) {
            context.ResetChargeState();
        }

        public sealed override void OnEnter(VaultStateMachine<GolemStateContext> machine, GolemStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<GolemStateContext> OnUpdate(VaultStateMachine<GolemStateContext> machine, GolemStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<GolemStateContext> machine, GolemStateContext ctx) {
            OnExit(ctx);
        }

        #region 地面运动工具

        /// <summary>是否踩地（速度为零且脚下有实体）</summary>
        protected static bool OnGround(NPC npc) {
            return npc.velocity.Y == 0f;
        }

        /// <summary>地面刹车</summary>
        protected static void GroundBrake(NPC npc, float rate = 0.8f) {
            if (npc.velocity.Y == 0f) {
                npc.velocity.X *= rate;
                if (Math.Abs(npc.velocity.X) < 0.1f) {
                    npc.velocity.X = 0f;
                }
            }
        }

        /// <summary>起跳：给定水平速度与竖直初速，穿地形直至越过目标头顶</summary>
        protected static void LaunchJump(NPC npc, float vx, float vy) {
            npc.velocity.X = vx;
            npc.velocity.Y = vy;
            npc.noTileCollide = true;
        }

        /// <summary>空中操舵：横向追踪目标，越过头顶后恢复碰撞下砸</summary>
        protected static void AirSteer(GolemStateContext ctx, float accel, float maxSpeed) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;

            //在目标正上方时收横速加坠速
            if (npc.position.X < target.position.X && npc.position.X + npc.width > target.position.X + target.width) {
                npc.velocity.X *= 0.9f;
                if (npc.Bottom.Y < target.position.Y) {
                    npc.velocity.Y += 0.24f;
                }
            }
            else {
                int dir = npc.Center.X < target.Center.X ? 1 : -1;
                npc.velocity.X += accel * dir;
                npc.velocity.X = MathHelper.Clamp(npc.velocity.X, -maxSpeed, maxSpeed);
            }

            RestoreTileCollide(ctx);
        }

        /// <summary>下落越过目标头顶后恢复地形碰撞（防穿底）</summary>
        protected static void RestoreTileCollide(GolemStateContext ctx) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;
            if (!npc.noTileCollide || !target.Alives()) {
                return;
            }
            if (npc.velocity.Y > 0f && npc.Bottom.Y > target.Top.Y) {
                npc.noTileCollide = false;
            }
            else if (Terraria.Collision.CanHit(npc.position, npc.width, npc.height, target.Center, 1, 1)
                && !Terraria.Collision.SolidCollision(npc.position, npc.width, npc.height)) {
                npc.noTileCollide = false;
            }
        }

        protected static Vector2 DirectionToTarget(GolemStateContext ctx) {
            return (ctx.Target.Center - ctx.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>难度伤害修正</summary>
        protected static int ScaleDamage(GolemStateContext ctx, int damage) {
            return GolemDirector.ScaleDamage(damage, ctx.DeathMode);
        }

        /// <summary>节奏帧压缩</summary>
        protected static int Tempo(GolemStateContext ctx, int frames) {
            return GolemDirector.Tempo(frames, ctx.DeathMode, ctx.Enraged);
        }

        #endregion
    }
}
