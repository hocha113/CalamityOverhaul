using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>一次借力施放的判定结果，代价结算在 <c>WraithPlayer.TryCastAbility</c> 统一进行</summary>
    public enum WraithCastResult : byte
    {
        /// <summary>条件不满足，未施放：不结算任何代价</summary>
        Fail,
        /// <summary>施放成功：结算双层代价（驾驭度磨损 + 侵蚀）</summary>
        Success,
        /// <summary>施放且犯戒（鬼律第十三条：规则转戒律）：双层代价外追加 <see cref="WraithAbility.TabooPenalty"/></summary>
        Taboo,
    }

    /// <summary>一次借力施放的上下文，owner 端组装</summary>
    public struct WraithAbilityContext
    {
        /// <summary>施放者（借力的持刀人，恒为本地玩家）</summary>
        public Player Player;
        /// <summary>载体物品（鬼切）</summary>
        public Item VesselItem;
        /// <summary>载体上的进度容器</summary>
        public WraithProgressStore Store;
        /// <summary>该鬼的进度记录（Bound 已由管线校验）</summary>
        public WraithProgressRecord Record;
        /// <summary>施放瞬间的光标世界坐标</summary>
        public Vector2 AimWorld;
        /// <summary>驾驭度快捷读数，效果强度按它缩放（鬼律：能力数值与驾驭度挂钩）</summary>
        public readonly float Mastery => Record?.Mastery ?? 0f;
    }

    /// <summary>
    /// 厉鬼赋力基类。实例为定义级单例（<see cref="WraithDefinition.Ability"/>），必须无状态：
    /// 冷却等每玩家数据在 <c>WraithPlayer</c>，进度写入由施放管线统一结算。
    /// 借力必有价（鬼律第十二条）：Success/Taboo 都会结算 <see cref="MasteryWear"/> 与 <see cref="ErosionCost"/>。<br/>
    /// 三段流水：<see cref="Cast"/>（owner 端判定与戒律自评）→
    /// <see cref="ExecuteWorld"/>（权威端世界改动，单人直呼/多人经 <c>WraithNet</c> 请求）→
    /// <see cref="PlayWorldFx"/>（各端本地演出，owner 即时、他端经广播）
    /// </summary>
    public abstract class WraithAbility
    {
        /// <summary>所属定义，<see cref="WraithDefinition.Ability"/> 缓存时回填</summary>
        public WraithDefinition Definition { get; internal set; }

        /// <summary>施放冷却（帧），每玩家独立计时</summary>
        public virtual int CooldownTicks => 60 * 5;
        /// <summary>身层代价：每次借力上涨的侵蚀量（0~1 尺度）</summary>
        public virtual float ErosionCost => 0.08f;
        /// <summary>刀层代价：每次借力的驾驭度磨损</summary>
        public virtual float MasteryWear => 0.01f;
        /// <summary>犯戒的驾驭度惩罚（叠加在磨损之上）</summary>
        public virtual float TabooPenalty => 0.06f;

        /// <summary>
        /// owner 端施放判定：校验目标/环境，自评戒律并经返回值上报。
        /// 不要在此改动世界（NPC/物块），那是 <see cref="ExecuteWorld"/> 的事
        /// </summary>
        public abstract WraithCastResult Cast(WraithAbilityContext ctx);

        /// <summary>
        /// 权威端世界改动（buff、生成物等）。单人 = 施放后直呼；
        /// 多人 = 服务器收到 <c>WraithNet</c> 请求后调用。mastery 随包传输，不回查物品
        /// </summary>
        public virtual void ExecuteWorld(Player caster, Vector2 aim, float mastery) { }

        /// <summary>世界侧演出，各端本地播放；服务器上不会被调用，无需 dedServ 防御</summary>
        public virtual void PlayWorldFx(Player caster, Vector2 aim) { }
    }
}
