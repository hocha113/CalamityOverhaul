using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>可复用的爆发曲线：快速上升到峰值，再进行指数衰减。</summary>
public static class SakuraBurstCurve
{
    /// <summary>返回完整爆发阶段内的 0–1 曲线值。</summary>
    public static float Evaluate(float time, float attackTime, float duration, float attackPower = .82f,
        float decayRate = 5.5f)
    {
        if (time <= 0f) return 0f;
        if (duration <= 0f || time >= duration) return 0f;
        attackTime = MathHelper.Clamp(attackTime, .001f, duration - .001f);
        if (time < attackTime)
            return Attack(time / attackTime, attackPower);
        return Decay((time - attackTime) / (duration - attackTime), decayRate);
    }

    /// <summary>快速上升段；幂值小于 1 时会更早到达峰值。</summary>
    public static float Attack(float normalizedTime, float power = .82f) =>
        MathF.Pow(MathHelper.Clamp(normalizedTime, 0f, 1f), Math.Max(.05f, power));

    /// <summary>归一化指数尾段：峰值为 1，结束时为 0。</summary>
    public static float Decay(float normalizedTime, float rate = 5.5f)
    {
        normalizedTime = MathHelper.Clamp(normalizedTime, 0f, 1f);
        rate = Math.Max(.01f, rate);
        float end = MathF.Exp(-rate);
        return (MathF.Exp(-rate * normalizedTime) - end) / (1f - end);
    }

    /// <summary>将归一化曲线值映射到指定数值区间。</summary>
    public static float Remap(float curveValue, float from, float to) =>
        MathHelper.Lerp(from, to, MathHelper.Clamp(curveValue, 0f, 1f));
}
