using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.CrimsonWitchs.Core
{
    /// <summary>红莲魔女状态索引，网络同步用；ID 一经发布不可改动，新状态只增不删</summary>
    internal enum WitchStateIndex : int
    {
        //====基础态====
        /// <summary>悬浮待机（M3 起兼任选招连接拍）</summary>
        HoverIdle = 0,
        /// <summary>屈膝礼离场（目标失效/远遁）</summary>
        Despawn = 1,
    }

    /// <summary>红莲魔女状态接口</summary>
    internal interface IWitchState : IVaultState<WitchStateContext>
    {
        /// <summary>状态索引，网络同步</summary>
        WitchStateIndex StateIndex { get; }

        /// <summary>进入状态时调用</summary>
        void OnEnter(WitchStateContext context);

        /// <summary>状态更新，每帧调用</summary>
        /// <returns>返回下一个状态，返回null表示保持当前状态</returns>
        IWitchState OnUpdate(WitchStateContext context);

        /// <summary>离开状态时调用</summary>
        void OnExit(WitchStateContext context);
    }

    /// <summary>红莲魔女状态基类</summary>
    internal abstract class WitchStateBase : VaultState<WitchStateContext>, IWitchState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract WitchStateIndex StateIndex { get; }

        public virtual void OnEnter(WitchStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IWitchState OnUpdate(WitchStateContext context);

        public virtual void OnExit(WitchStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<WitchStateContext> machine, WitchStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<WitchStateContext> OnUpdate(VaultStateMachine<WitchStateContext> machine, WitchStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<WitchStateContext> machine, WitchStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>平滑移动到目标点（惯性插值悬浮）</summary>
        protected static void MoveTo(NPC npc, Vector2 target, float speed, float inertia) {
            Vector2 direction = target - npc.Center;
            if (direction.Length() > 0.01f) {
                direction.Normalize();
            }
            Vector2 desiredVelocity = direction * speed;
            npc.velocity = (npc.velocity * (1f - inertia)) + (desiredVelocity * inertia);
        }

        /// <summary>面向目标（人形直立，仅翻转朝向并随速度轻微倾身）</summary>
        protected static void FaceTarget(NPC npc, Vector2 targetCenter) {
            npc.direction = npc.spriteDirection = npc.Center.X < targetCenter.X ? 1 : -1;
            npc.rotation = npc.velocity.X * 0.02f;
        }

        /// <summary>获取到玩家的方向向量</summary>
        protected static Vector2 GetDirectionToTarget(WitchStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>启用接触伤害（仅圆舞突进类体术使用）</summary>
        protected static void EnableContactDamage(NPC npc) {
            npc.damage = npc.defDamage;
        }

        /// <summary>禁用接触伤害（默认态：她是施法者）</summary>
        protected static void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        #endregion
    }
}
