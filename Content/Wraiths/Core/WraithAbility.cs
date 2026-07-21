using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>借力判定结果，代价在 <c>WraithPlayer.TryCastAbility</c> 统一结算</summary>
    public enum WraithCastResult : byte
    {
        /// <summary>未施放，不计代价</summary>
        Fail,
        /// <summary>成功，结算磨损+侵蚀</summary>
        Success,
        /// <summary>犯戒，额外 <see cref="WraithAbility.TabooPenalty"/></summary>
        Taboo,
    }

    /// <summary>借力上下文，owner 端组装</summary>
    public struct WraithAbilityContext
    {
        /// <summary>施放者，恒本地玩家</summary>
        public Player Player;
        /// <summary>载体物品</summary>
        public Item VesselItem;
        /// <summary>进度容器</summary>
        public WraithProgressStore Store;
        /// <summary>进度记录，Bound 已校验</summary>
        public WraithProgressRecord Record;
        /// <summary>光标世界坐标</summary>
        public Vector2 AimWorld;
        /// <summary>驾驭度，效果强度缩放</summary>
        public readonly float Mastery => Record?.Mastery ?? 0f;
    }

    /// <summary>
    /// 赋力基类，定义级单例无状态；冷却在 <c>WraithPlayer</c>。<br/>
    /// 流水 <see cref="Cast"/> → <see cref="ExecuteWorld"/> → <see cref="PlayWorldFx"/>
    /// </summary>
    public abstract class WraithAbility
    {
        /// <summary>所属定义，缓存时回填</summary>
        public WraithDefinition Definition { get; internal set; }

        /// <summary>冷却帧，每玩家独立</summary>
        public virtual int CooldownTicks => 60 * 5;
        /// <summary>侵蚀上涨量 0~1</summary>
        public virtual float ErosionCost => 0.08f;
        /// <summary>驾驭度磨损</summary>
        public virtual float MasteryWear => 0.01f;
        /// <summary>犯戒额外磨损</summary>
        public virtual float TabooPenalty => 0.06f;

        /// <summary>owner 端判定与戒律自评，勿改世界</summary>
        public abstract WraithCastResult Cast(WraithAbilityContext ctx);

        /// <summary>权威端世界改动；多人经 <c>WraithNet</c>，mastery 随包不回查</summary>
        public virtual void ExecuteWorld(Player caster, Vector2 aim, float mastery) { }

        /// <summary>各端本地演出；服务器不调</summary>
        public virtual void PlayWorldFx(Player caster, Vector2 aim) { }
    }
}
