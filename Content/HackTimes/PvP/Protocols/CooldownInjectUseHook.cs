using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 冷却注入的落点乘区。<c>UseSpeedMultiplier</c> 是物品级整体使用速度乘子，
    /// tML 把它同时乘进 useTime 与 useAnimation——近战/远程/魔法/召唤/盗贼
    /// 乃至工具与放置全部经它，一个钩子覆盖全 DamageClass，
    /// 不用挨个类别写攻速。速度 ×1/(1+f) 即时间 ×(1+f)，f 已在协议侧过
    /// <see cref="HackPvPRules.ClampUseSlow"/> 红线。<br/>
    /// 真值只在防守方本机（帐本），远端与服务端此乘子恒 1——
    /// 远端看到的挥舞动画节奏由物品使用的常规同步链自然对齐
    /// </summary>
    internal sealed class CooldownInjectUseHook : GlobalItem
    {
        public override float UseSpeedMultiplier(Item item, Player player) {
            float fraction = CooldownInject.GetSlowFraction(player);
            return fraction > 0f ? 1f / (1f + fraction) : 1f;
        }
    }
}
