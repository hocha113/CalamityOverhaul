using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Shared game/preview timing. Positions are world coordinates; camera values are screen origins.</summary>
public static class SakuraDashMotion
{
    public static int TravelFrames => 7;
    public static int ReappearFrames =>  4;
    public static int AfterimageFrames =>  12;
    public static int CameraMoveFrames =>  40;
    public static float PathSampleStep => 5f;
    public static float ObstacleBackoff => 16f;

    public static float Ease(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    // 只有冲击特效沿路径行进，角色第一帧就闪现至已验证的终点。
    public static Vector2 ImpactPosition(Vector2 start, Vector2 end, float frame) =>
        Vector2.Lerp(start, end, MathHelper.Clamp(frame / TravelFrames, 0f, 1f));

    // 与参考一致：Lerp(实时跟随目标, 闪现前镜头, 剩余时间比例的平方)。
    // 40 -> 0 帧，先快后慢；比例归一化避免修改时长后出现外插。
    public static Vector2 Camera(Vector2 startScreen, Vector2 targetScreen, float remainingFrames)
    {
        float remaining = MathHelper.Clamp(remainingFrames / CameraMoveFrames, 0f, 1f);
        return Vector2.Lerp(targetScreen, startScreen, remaining * remaining);
    }

    public static float Appearance(float frame) => MathHelper.Clamp((frame - TravelFrames + 1f) / ReappearFrames, 0f, 1f);
    public static float EchoOpacity(float frame, int index) =>
        MathF.Pow(1f - MathHelper.Clamp((frame - TravelFrames) / AfterimageFrames, 0f, 1f), 2f)
        * (.32f - index * .07f);

    public static float EchoDistance(float frame, int index) =>
        (index + 1) * 22f * (1f - Ease((frame - TravelFrames) / AfterimageFrames));

    // 首帧沿路径做单点采样，碰到第一个阻挡就从上一采样点退让。不检测玩家宽高。
    public static Vector2 ResolveLanding(Vector2 start, Vector2 target, Func<Vector2, bool> isPointClear)
    {
        Vector2 delta = target - start;
        float length = delta.Length();
        if (length <= .001f || !isPointClear(start)) return start;
        Vector2 direction = delta / length;
        float lastClearDistance = 0f;
        int steps = Math.Max(1, (int)MathF.Ceiling(length / PathSampleStep));
        for (int i = 1; i <= steps; i++)
        {
            float distance = Math.Min(length, i * PathSampleStep);
            Vector2 candidate = start + direction * distance;
            if (!isPointClear(candidate))
                return start + direction * Math.Max(0f, lastClearDistance - ObstacleBackoff);
            lastClearDistance = distance;
        }
        return target;
    }
}
