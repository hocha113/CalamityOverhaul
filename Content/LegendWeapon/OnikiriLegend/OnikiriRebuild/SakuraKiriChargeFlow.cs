using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal readonly record struct SakuraKiriFlowSettings(
    float Radius, float Arc, float Width, float Speed, float Brightness, int Segments);

internal static class SakuraKiriChargeFlow
{
    internal static float Radius => 420f;
    internal static float Arc => 425f;
    internal static float Width => 32f;
    internal static float Speed => 1.65f;
    internal static float Brightness => 2.55f;
    internal static int Segments => 30;
    internal static float FirstStreamProgress => .035f;
    internal static float StreamIntervalProgress => .105f;
    internal static float StreamFadeProgress => .14f;
    internal static float ChargeRampPower => .7f;
    internal static float ChargeStartIntensity => .45f;

    internal static float StreamStartProgress(int stream)
    {
        int order = stream switch
        {
            5 => 0, 0 => 1, 7 => 2, 2 => 3,
            1 => 4, 4 => 5, 3 => 6, 6 => 7,
            _ => 8
        };
        return FirstStreamProgress + order * StreamIntervalProgress;
    }

    internal static float StreamStrength(int stream, float progress)
    {
        float age = (progress - StreamStartProgress(stream)) / Math.Max(.001f, StreamFadeProgress);
        float charge = MathF.Pow(Smooth(progress), Math.Max(.01f, ChargeRampPower));
        return Smooth(age) * MathHelper.Lerp(MathHelper.Clamp(ChargeStartIntensity, 0f, 1f), 1f, charge);
    }

    internal static Vector2 GetSwordTarget(Texture2D weapon, Vector2 playerCenter, int facing, int gravity)
    {
        Vector2 origin = new(weapon.Width * .5f, weapon.Height * .5f);
        Vector2 grip = new(23f, 206f);
        Vector2 localGrip = new((grip.X - origin.X) * facing, (grip.Y - origin.Y) * gravity);
        Vector2 offset = SakuraReadyStanceWeaponVisual.WeaponOffset;
        float rotation = SakuraReadyStanceWeaponVisual.WeaponRotation * facing * gravity;
        return playerCenter + new Vector2(offset.X * facing, offset.Y * gravity)
            + Vector2.Transform(localGrip, Matrix.CreateRotationZ(rotation));
    }

    internal static void GetCurve(Vector2 target, int facing, int gravity, int stream,
        SakuraKiriFlowSettings settings, out Vector2 p0, out Vector2 p1, out Vector2 p2, out Vector2 p3)
    {
        int row = stream / 2;
        int side = (stream & 1) == 0 ? -facing : facing;
        int sweep = side * facing;
        float spread = row - 1.5f;
        p0 = target + new Vector2(side * settings.Radius * (1f + row * .12f),
            (spread * 92f + sweep * 25f) * gravity);
        p1 = target + new Vector2(side * settings.Radius * .48f,
            (-spread * settings.Arc + sweep * settings.Arc * .82f) * gravity);
        p2 = target + new Vector2(-side * settings.Radius * .16f,
            (spread * settings.Arc * .55f - sweep * settings.Arc * .62f) * gravity);
        p3 = target + new Vector2((row - 1.5f) * 6f * facing,
            (sweep * 9f + spread * 3f) * gravity);
    }

    internal static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float s = 1f - t;
        return a * (s * s * s) + b * (3f * s * s * t)
            + c * (3f * s * t * t) + d * (t * t * t);
    }

    internal static Vector2 Tangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float s = 1f - t;
        return 3f * (b - a) * (s * s) + 6f * (c - b) * (s * t) + 3f * (d - c) * (t * t);
    }

    internal static float Fract(float value) => value - MathF.Floor(value);
    internal static float Smooth(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
