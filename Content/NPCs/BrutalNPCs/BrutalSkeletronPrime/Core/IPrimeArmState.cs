using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>机械臂状态索引，写入 npc.ai[2] 网络同步；四臂共用 PrimeArmStateContext，百位区分归属</summary>
    internal enum PrimeArmStateIndex : int
    {
        //激光炮 100+
        LaserAim = 100,
        LaserRapidFire = 101,
        LaserChargedShot = 102,
        LaserRing = 103,
        LaserSweep = 104,
        LaserTriShot = 105,
        //火箭炮 200+
        CannonBombard = 200,
        CannonSpread = 201,
        CannonMortar = 202,
        CannonDirect = 203,
        //电锯 300+
        SawIdle = 300,
        SawSpinUp = 301,
        SawDash = 302,
        SawOrbit = 303,
        SawDrill = 304,
        SawRecovery = 305,
        SawBoomerang = 306,
        SawGroundCut = 307,
        //钳爪 400+
        ViceIdle = 400,
        ViceWindUp = 401,
        ViceStrike = 402,
        ViceRecovery = 403,
        ViceCombo = 404,
        ViceReturn = 405,
        ViceTripleLunge = 406,
        ViceClapWave = 407,
        ViceExecutionLunge = 408,
    }

    /// <summary>机械臂状态基类，弹簧/瞄准/跟随工具</summary>
    internal abstract class PrimeArmStateBase : VaultState<PrimeArmStateContext>, IVaultState<PrimeArmStateContext>
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract PrimeArmStateIndex StateIndex { get; }

        public virtual void OnEnter(PrimeArmStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract PrimeArmStateBase OnUpdate(PrimeArmStateContext context);

        public virtual void OnExit(PrimeArmStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<PrimeArmStateContext> machine, PrimeArmStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<PrimeArmStateContext> OnUpdate(VaultStateMachine<PrimeArmStateContext> machine, PrimeArmStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<PrimeArmStateContext> machine, PrimeArmStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>弹簧移动，SpringVelocity跨状态</summary>
        protected static void SpringMove(PrimeArmStateContext ctx, Vector2 target, float speedMult,
            float stiffness = 0.17f, float damping = 0.83f, float maxSpeed = 29f) {
            Vector2 toTarget = target - ctx.Npc.Center;
            Vector2 velocity = ctx.SpringVelocity;
            velocity += toTarget * stiffness * speedMult;
            velocity *= damping;
            if (velocity.LengthSquared() > maxSpeed * maxSpeed) {
                velocity = velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            ctx.SpringVelocity = velocity;
            ctx.Npc.velocity = velocity;
        }

        /// <summary>伺服转角最短弧</summary>
        protected static void ServoRotate(NPC npc, float targetRotation, float maxStep) {
            float diff = MathHelper.WrapAngle(targetRotation - npc.rotation);
            npc.rotation += MathHelper.Clamp(diff, -maxStep, maxStep);
        }

        /// <summary>伺服指向某个世界坐标</summary>
        protected static void ServoAimAt(NPC npc, Vector2 worldTarget, float maxStep) {
            float targetRotation = (worldTarget - npc.Center).ToRotation() - MathHelper.PiOver2;
            ServoRotate(npc, targetRotation, maxStep);
        }

        /// <summary>伺服瞄准玩家</summary>
        protected static void SmoothAim(PrimeArmStateContext ctx, float smoothness) {
            NPC npc = ctx.Npc;
            Vector2 toPlayer = ctx.Target.Center - npc.Center;
            ctx.AimDirection = Vector2.Lerp(ctx.AimDirection, toPlayer.SafeNormalize(Vector2.UnitX), smoothness);
            if (ctx.AimDirection == Vector2.Zero) {
                ctx.AimDirection = Vector2.UnitX;
            }

            float targetRotation = ctx.AimDirection.ToRotation() - MathHelper.PiOver2;
            //后坐抖动，确定性正弦
            if (ctx.RecoilIntensity > 1f) {
                float jitter = (float)System.Math.Sin(Main.GameUpdateCount * 0.83f + npc.whoAmI) * 0.1f;
                targetRotation += jitter * (ctx.RecoilIntensity / 10f);
            }
            ServoRotate(npc, targetRotation, smoothness * 1.2f);
        }

        /// <summary>分轴跟随头锚</summary>
        protected static void AnchoredFollow(PrimeArmStateContext ctx, float anchorYTop, float anchorYBottom,
            float anchorXLeft, float anchorXRight) {
            NPC npc = ctx.Npc;
            NPC head = ctx.Head;

            float acceleration = ctx.BossRush ? 0.6f
                : ctx.Death ? (ctx.MasterMode ? 0.375f : 0.3f)
                : (ctx.MasterMode ? 0.3125f : 0.25f);
            float accelerationMult = 1f;
            int missing = ctx.MissingPartnerCount;
            acceleration += missing * 0.025f;
            if (missing > 0) {
                accelerationMult += 0.5f;
            }
            if (ctx.MasterMode) {
                acceleration *= accelerationMult;
            }

            //后坐力反推
            if (ctx.RecoilIntensity > 0.5f) {
                npc.velocity -= ctx.AimDirection * (ctx.RecoilIntensity * 0.3f);
            }

            float topVelocity = acceleration * 100f;
            float deceleration = ctx.MasterMode ? 0.6f : 0.8f;

            if (npc.position.Y > head.position.Y + anchorYTop) {
                if (npc.velocity.Y > 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) {
                    npc.velocity.Y = topVelocity;
                }
            }
            else if (npc.position.Y < head.position.Y + anchorYBottom) {
                if (npc.velocity.Y < 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) {
                    npc.velocity.Y = -topVelocity;
                }
            }

            if (npc.Center.X > head.Center.X + anchorXRight) {
                if (npc.velocity.X > 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X -= acceleration;
                if (npc.velocity.X > topVelocity) {
                    npc.velocity.X = topVelocity;
                }
            }
            if (npc.Center.X < head.Center.X + anchorXLeft) {
                if (npc.velocity.X < 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X += acceleration;
                if (npc.velocity.X < -topVelocity) {
                    npc.velocity.X = -topVelocity;
                }
            }
        }

        /// <summary>到待机锚点距离，拉回过远用</summary>
        protected static float IdleAnchorDistance(PrimeArmStateContext ctx) {
            Vector2 anchor = ctx.Head.Center + new Vector2(-200f * ctx.Side, 230f - ctx.Head.height * 0.5f);
            return ctx.Npc.Distance(anchor);
        }

        protected static int ScaleDamage(int damage) => HeadPrimeAI.SetMultiplier(damage);

        #endregion
    }
}
