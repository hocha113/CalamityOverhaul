using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>手持斩击与剑气斩共用的材质和原速时间轴。倍率只推进时间，不改 Shader 参数。</summary>
internal static class SakuraSlashStyle
{
    public const string TexturePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/Eff_Trail_63_03";
    public const string NoisePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/PerlinX";
    public const string BladePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/Eff_Trail_63_03_Blade";
    public const float PreparationDuration = .05f;
    public const float AttackDuration = .045f;
    public const float DriftDuration = .2f;
    public const float DissolveDelay = .10f;
    public const float DissolveRate = 2.8f;
    public const float PetalEmissionDuration = .25f;
    public static float Lifetime => Math.Max(PreparationDuration + AttackDuration + DriftDuration,
        Math.Max(DissolveDelay + 1f / DissolveRate, PreparationDuration + PetalEmissionDuration));

    public static void Apply(SakuraSlashInstance slash, float speedMultiplier)
    {
        slash.SpeedMultiplier = speedMultiplier;
        slash.AutoFade = false;
        slash.AutoSweep = slash.AutoDissolve = true;
        slash.NoiseOnlyDissolve = false;
        slash.AttackUvStart = -1.1f;
        slash.AttackUvEnd = -.25f;
        slash.PreparationDuration = PreparationDuration;
        slash.PreparationUvFraction = .5f;
        slash.AttackDuration = AttackDuration;
        slash.DriftDuration = DriftDuration;
        slash.DriftUvDistance = .2f;
        slash.DissolveDelay = DissolveDelay;
        slash.DissolveRate = DissolveRate;
        slash.DissolveNoiseStrength = .5f;
        slash.DissolveUvScale = new Vector2(.25f, .5f);
        slash.DissolveEdgeWidth = .15f;
        slash.DissolveRimWidth = .001f;
        slash.DissolveEdgeColor = new Vector3(3.5f, .32f, .32f);
        slash.DissolveEdgeIntensity = 1.2f;
        slash.PetalTangentialSpeed = 260f;
        slash.PetalOutwardSpeed = 40f;
        slash.PetalCount = 28;
        slash.PetalEmissionDuration = PetalEmissionDuration;
        slash.PetalEndDensity = 2f;
        slash.PetalScatterWidth = 0f;
    }

    public static void Apply(SakuraSlashRenderer renderer)
    {
        renderer.ForceAlphaBlend = true;
        renderer.TintColor = new Vector3(1f, .2f, .2f);
        renderer.BladeColor = Vector3.One;
    }
}
