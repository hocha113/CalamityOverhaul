namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>World pixels, seconds and radians. Direction is supplied per emission.</summary>
public readonly record struct SakuraEmissionSettings
{
    public int FineCount { get; init; } = 10;
    public int SimpleCount { get; init; } = 48;
    public float Speed { get; init; } = 620;
    public float Spread { get; init; } = .48f;
    public float SpawnRadius { get; init; } = 18;
    /// <summary>1 uses the approved small size (formerly Scale 0.3).</summary>
    public float Scale { get; init; } = 1;
    public float Lifetime { get; init; } = 2.8f;
    public float Drag { get; init; } = 1.15f;
    public float Gravity { get; init; } = 58;
    public float Curl { get; init; } = 125;
    /// <summary>是否从中心向三维球面方向发射；投影到屏幕后仍保留深度和姿态差异。</summary>
    public bool SphericalBurst { get; init; }
    /// <summary>球形爆发的透视放大强度；只影响花瓣尺寸，不改变深度排序。</summary>
    public float PerspectiveStrength { get; init; } = 0;
    public SakuraEmissionSettings() { }
    public static SakuraEmissionSettings Default => new();
}
