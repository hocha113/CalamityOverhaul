using System;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>60 Hz 原速时间轴；浮点推进，区间命中保证高攻速跨帧时不漏判。</summary>
internal static class SakuraPlayback
{
    public const float FramesPerSecond = 60f;
    // 限制整体播放倍率；手工时间轴允许同 tick 跨过多刀，逐刀检测窗口。
    public static float ClampSpeed(float value, float fallback = 1f) =>
        float.IsFinite(value) && value > 0 ? Math.Clamp(value, .05f, 8f) : fallback;

    public static bool CrossesWindow(float previous, float current, float start, float end) =>
        current >= start && previous <= end && current > previous;
}
