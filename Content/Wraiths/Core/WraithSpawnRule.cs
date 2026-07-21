using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>生成判定上下文，候选玩家由调度器随机挑</summary>
    public struct WraithSpawnContext
    {
        /// <summary>候选玩家</summary>
        public Player Player;
        /// <summary>被判定定义</summary>
        public WraithDefinition Definition;
    }

    /// <summary>自动显形规则；定义返回 null 则只能外部显式生成</summary>
    public sealed class WraithSpawnRule
    {
        /// <summary>条件谓词，null=恒真</summary>
        public Func<WraithSpawnContext, bool> Condition;
        /// <summary>额外概率 0~1</summary>
        public float ChancePerCheck = 0.25f;
        /// <summary>成功后冷却帧</summary>
        public int CooldownTicks = 3600;
        /// <summary>同屏实体上限</summary>
        public int MaxAlive = 1;
        /// <summary>落点选择，null=默认环带；返回 null=本轮放弃</summary>
        public Func<WraithSpawnContext, Vector2?> PositionPicker;
    }

    /// <summary>通用生成条件谓词</summary>
    public static class WraithSpawnConditions
    {
        /// <summary>夜间</summary>
        public static bool Night(WraithSpawnContext ctx) => !Main.dayTime;
        /// <summary>血月</summary>
        public static bool BloodMoon(WraithSpawnContext ctx) => Main.bloodMoon;
        /// <summary>地表以下</summary>
        public static bool Underground(WraithSpawnContext ctx) => ctx.Player.Center.Y > Main.worldSurface * 16.0;
        /// <summary>地表或以上</summary>
        public static bool Overworld(WraithSpawnContext ctx) => ctx.Player.Center.Y <= Main.worldSurface * 16.0;
        /// <summary>生命低于给定比例</summary>
        public static Func<WraithSpawnContext, bool> HealthBelow(float ratio)
            => ctx => ctx.Player.statLife < ctx.Player.statLifeMax2 * ratio;
        /// <summary>谓词全成立</summary>
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
