using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>一次生成判定的上下文，候选玩家由调度器随机挑选</summary>
    public struct WraithSpawnContext
    {
        /// <summary>候选玩家，显形位置围绕其展开</summary>
        public Player Player;
        /// <summary>被判定的定义</summary>
        public WraithDefinition Definition;
    }

    /// <summary>
    /// 自动显形规则，纯机械参数。定义不提供规则（返回 null）则该厉鬼只能被外部显式生成
    /// </summary>
    public sealed class WraithSpawnRule
    {
        /// <summary>生成条件谓词，null 视为恒真；主题条件由现成谓词组合，不进框架</summary>
        public Func<WraithSpawnContext, bool> Condition;
        /// <summary>条件通过后的额外概率 0~1</summary>
        public float ChancePerCheck = 0.25f;
        /// <summary>成功生成后的冷却帧数</summary>
        public int CooldownTicks = 3600;
        /// <summary>该定义的同屏实体上限</summary>
        public int MaxAlive = 1;
        /// <summary>落点选择，null 走默认的玩家屏幕外环带；返回 null 表示本次放弃</summary>
        public Func<WraithSpawnContext, Vector2?> PositionPicker;
    }

    /// <summary>通用生成条件谓词，供规则组合</summary>
    public static class WraithSpawnConditions
    {
        /// <summary>夜间</summary>
        public static bool Night(WraithSpawnContext ctx) => !Main.dayTime;
        /// <summary>血月</summary>
        public static bool BloodMoon(WraithSpawnContext ctx) => Main.bloodMoon;
        /// <summary>候选玩家位于地表以下</summary>
        public static bool Underground(WraithSpawnContext ctx) => ctx.Player.Center.Y > Main.worldSurface * 16.0;
        /// <summary>候选玩家位于地表或以上</summary>
        public static bool Overworld(WraithSpawnContext ctx) => ctx.Player.Center.Y <= Main.worldSurface * 16.0;
        /// <summary>候选玩家生命低于给定比例</summary>
        public static Func<WraithSpawnContext, bool> HealthBelow(float ratio)
            => ctx => ctx.Player.statLife < ctx.Player.statLifeMax2 * ratio;
        /// <summary>多个谓词全部成立</summary>
        public static Func<WraithSpawnContext, bool> All(params Func<WraithSpawnContext, bool>[] conditions)
            => ctx => {
                foreach (var condition in conditions) {
                    if (condition != null && !condition(ctx)) {
                        return false;
                    }
                }
                return true;
            };
    }
}
