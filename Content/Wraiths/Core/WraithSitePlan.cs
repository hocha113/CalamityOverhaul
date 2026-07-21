using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>据点判定上下文；锚定前 Anchor 无意义</summary>
    public struct WraithSiteContext
    {
        /// <summary>被判定的定义</summary>
        public WraithDefinition Definition;
        /// <summary>候选玩家，可能 null</summary>
        public Player Candidate;
        /// <summary>已锚定中心，世界坐标</summary>
        public Vector2 Anchor;
    }

    /// <summary>
    /// 据点机械参数；状态存 <c>WraithSiteSystem</c>，调度在 <c>WraithDirector</c>
    /// </summary>
    public sealed class WraithSitePlan
    {
        /// <summary>动态选点，null=只能手工落锚；本轮无点也返回 null</summary>
        public Func<WraithSiteContext, Vector2?> AnchorPicker;
        /// <summary>活化谓词，null=恒真</summary>
        public Func<WraithSiteContext, bool> ActivationCondition;
        /// <summary>触发半径 px，有存活玩家入圈才显形</summary>
        public float TriggerRadius = 1100f;
        /// <summary>事件结束后再活化冷却帧</summary>
        public int CooldownTicks = 60 * 60 * 3;
        /// <summary>锚定失败重试间隔帧</summary>
        public int AnchorRetryTicks = 60 * 30;
    }

    /// <summary>三幕复苏谓词，见 WRAITHS-DESIGN.md 第四节</summary>
    public static class WraithActs
    {
        /// <summary>一幕，困难模式前</summary>
        public static bool ActOne => !Main.hardMode;
        /// <summary>二幕，入困难模式</summary>
        public static bool ActTwo => Main.hardMode;
        /// <summary>三幕，DoG 陨；无灾厄回落月总裁</summary>
        public static bool ActThree => CWRRef.Has ? CWRRef.GetDownedDoG() : NPC.downedMoonlord;
    }
}
