using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>一次据点判定的上下文；锚定阶段 Anchor 尚无意义，取 <see cref="Vector2.Zero"/></summary>
    public struct WraithSiteContext
    {
        /// <summary>被判定的定义</summary>
        public WraithDefinition Definition;
        /// <summary>候选玩家，锚定选点与活化判距围绕其展开，可能为 null（无人在线时不评估）</summary>
        public Player Candidate;
        /// <summary>已锚定的据点中心（世界坐标）</summary>
        public Vector2 Anchor;
    }

    /// <summary>
    /// 据点计划：厉鬼怪谈据点的机械参数（鬼律第五条，正典鬼的唯一出现通道）。
    /// 据点状态（锚位/冷却/事件计数）与世界存档在 <c>WraithSiteSystem</c>，
    /// 调度（活化评估与显形）在 <c>WraithDirector</c>
    /// </summary>
    public sealed class WraithSitePlan
    {
        /// <summary>
        /// 动态锚定选点，返回据点中心；null = 只能被外部手工落锚
        /// （<c>WraithSiteSystem.Plant</c>，剧情/结构/调试路径）。返回 null 表示本轮未找到合适位置
        /// </summary>
        public Func<WraithSiteContext, Vector2?> AnchorPicker;
        /// <summary>活化条件（幕次、天候、时段等谓词组合），null 视为恒真</summary>
        public Func<WraithSiteContext, bool> ActivationCondition;
        /// <summary>触发半径（像素）：有存活玩家进入锚点此距离内才显形</summary>
        public float TriggerRadius = 1100f;
        /// <summary>一场据点事件结束后（实体离场）的再活化冷却帧数</summary>
        public int CooldownTicks = 60 * 60 * 3;
        /// <summary>锚定失败后的重试间隔帧数</summary>
        public int AnchorRetryTicks = 60 * 30;
    }

    /// <summary>三幕复苏时间轴谓词（幕次归属见 WRAITHS-DESIGN.md 第四节）</summary>
    public static class WraithActs
    {
        /// <summary>一幕：困难模式前，只有传闻与异象</summary>
        public static bool ActOne => !Main.hardMode;
        /// <summary>二幕：入困难模式，首批据点活化</summary>
        public static bool ActTwo => Main.hardMode;
        /// <summary>三幕：灾厄后期深层复苏，DoG 已陨；无灾厄时回落月总裁</summary>
        public static bool ActThree => CWRRef.Has ? CWRRef.GetDownedDoG() : NPC.downedMoonlord;
    }
}
