using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>手部状态索引，写入手 npc.ai[2] 网络同步</summary>
    internal enum SkeletronHandStateIndex : int
    {
        /// <summary>侧翼护卫浮游</summary>
        Guard = 0,
        /// <summary>砸击连段（内部含双拍与合击）</summary>
        Crush = 1,
        /// <summary>合拍钳杀</summary>
        Clap = 2,
        /// <summary>旋杀紧缩环绕</summary>
        Orbit = 3,
        /// <summary>断手狂化撕臂殉解</summary>
        Torn = 4,
    }

    /// <summary>手部状态上下文</summary>
    internal class SkeletronHandContext : INpcStateContext
    {
        public NPC Npc { get; set; }
        public NPC Head { get; set; }
        public Player Target { get; set; }
        public SkeletronHandAI Owner { get; set; }

        public bool BossRush { get; set; }
        public bool MasterMode { get; set; }
        public bool Death { get; set; }
        /// <summary>-1 左 / 1 右</summary>
        public int Side { get; set; }

        /// <summary>弹簧速度缓存（伺服追踪）</summary>
        public Vector2 SpringVelocity { get; set; }
        /// <summary>锁链张紧度 0松弛~1绷直，绘制层消费</summary>
        public float ChainTension { get; set; }
        /// <summary>掌心幽火强度 0~1，绘制层消费</summary>
        public float PalmFlame { get; set; }
    }

    /// <summary>手部状态基类</summary>
    internal abstract class SkeletronHandStateBase : VaultState<SkeletronHandContext>
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract SkeletronHandStateIndex StateIndex { get; }

        public virtual void OnEnter(SkeletronHandContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract SkeletronHandStateBase OnUpdate(SkeletronHandContext context);

        public virtual void OnExit(SkeletronHandContext context) {
            context.ChainTension = 0f;
            context.PalmFlame = 0f;
        }

        public sealed override void OnEnter(VaultStateMachine<SkeletronHandContext> machine, SkeletronHandContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<SkeletronHandContext> OnUpdate(VaultStateMachine<SkeletronHandContext> machine, SkeletronHandContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<SkeletronHandContext> machine, SkeletronHandContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>弹簧追踪目标点（软追踪，柔中带滞）</summary>
        protected static void SpringMove(SkeletronHandContext ctx, Vector2 toPoint, float stiffness = 0.16f, float damping = 0.84f, float maxSpeed = 30f) {
            NPC npc = ctx.Npc;
            Vector2 vel = ctx.SpringVelocity;
            vel += (toPoint - npc.Center) * stiffness * 0.1f;
            vel *= damping;
            if (vel.Length() > maxSpeed) {
                vel = vel.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            ctx.SpringVelocity = vel;
            npc.velocity = vel;
        }

        /// <summary>掌心朝向目标（rotation 约定：掌根在上，指尖指向 rotation-PiOver2 方向）</summary>
        protected static void AimPalm(NPC npc, Vector2 target, float rate = 0.12f) {
            float want = (target - npc.Center).ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(want, rate);
        }

        /// <summary>护卫锚点：头侧翼下垂位</summary>
        protected static Vector2 GuardAnchor(SkeletronHandContext ctx, float bobSeed = 0f) {
            float bob = (float)System.Math.Sin((Main.GameUpdateCount + ctx.Npc.whoAmI * 41 + bobSeed) * 0.041f) * 14f;
            return ctx.Head.Center + new Vector2(-210f * ctx.Side, 195f + bob);
        }

        #endregion
    }
}
