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
        /// <summary>合掌拍捉伺服（读头侧子相位行动）</summary>
        Snatch = 5,
        /// <summary>嘲讽鼓掌伺服（读头侧子相位行动）</summary>
        Applaud = 6,
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

        /// <summary>轴向挤压偏移（渲染层阻尼弹簧：+挤扁 -拉伸）</summary>
        public float SquashOff { get; set; }
        /// <summary>轴向挤压速度</summary>
        public float SquashVel { get; set; }

        /// <summary>注入一次挤压冲量（正=沿指轴压扁回弹，负=拉伸）</summary>
        public void TriggerSquash(float impulse) {
            SquashVel = MathHelper.Clamp(SquashVel + impulse, -0.9f, 0.9f);
        }
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

        /// <summary>掌心朝向目标 + 腕部附加角（偏移并进 want 再插值；
        /// 若在 AngleLerp 之后逐帧加偏移，平衡点=偏移/速率会被反馈放大数倍）</summary>
        protected static void AimPalmOffset(NPC npc, Vector2 target, float offset, float rate = 0.12f) {
            float want = (target - npc.Center).ToRotation() + MathHelper.PiOver2 + offset;
            npc.rotation = npc.rotation.AngleLerp(want, rate);
        }

        /// <summary>护卫锚点：头侧翼下垂位，8字懒游（Lissajous），比纯正弦活
        /// （侧别与攻击态槽位同号：Side=-1 在左，臂骼渲染后交叉臂读作错位，故不沿用原版反号习惯）</summary>
        protected static Vector2 GuardAnchor(SkeletronHandContext ctx, float bobSeed = 0f) {
            float t = (Main.GameUpdateCount + ctx.Npc.whoAmI * 41 + bobSeed) * 0.023f;
            float swayX = (float)System.Math.Sin(t) * 24f * ctx.Side;
            float swayY = (float)System.Math.Sin(t * 2f + 0.7f) * 13f;
            return ctx.Head.Center + new Vector2(210f * ctx.Side + swayX, 195f + swayY);
        }

        /// <summary>头侧编队时钟（各端确定性自增），取不到覆写时回退全局帧</summary>
        protected static float FormationClock(SkeletronHandContext ctx) {
            ctx.Head.TryGetOverride(out SkeletronHeadAI headOverride);
            return headOverride?.ai[SkeletronAiSlots.OverrideOrbitClock] ?? Main.GameUpdateCount;
        }

        #endregion
    }
}
