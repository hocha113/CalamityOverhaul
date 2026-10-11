using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>A local visual handle. Update its pose from the owning weapon/projectile.</summary>
public sealed class SakuraSlashInstance
{
    internal SakuraSlashPetalBurst PetalBurst { get; }
    private Vector2 position;
    private Matrix localTransform;
    private float opacity = 1;
    internal static readonly Vector2 DefaultDissolveUvScale = new(3.2f, 1.35f);
    private Vector2 dissolveUvScale = DefaultDissolveUvScale;
    private float dissolveNoiseStrength = .22f;
    private Vector2 dissolveDirection = new(0, -1);
    private bool noiseOnlyDissolve;
    private readonly float minimumLifetime;
    private float dissolveRate = 1f / .9f, dissolveDelay, attackDuration = .055f, dissolveEdgeWidth = .055f;
    private float dissolveRimWidth = .025f, dissolveEdgeIntensity = 1f;
    private Vector3 dissolveEdgeColor = new(1f, .48f, .72f);
    private float attackUvStart = -1.02f, attackUvEnd;
    private float preparationDuration = .04f, preparationUvFraction = .12f;
    private float driftDuration = .55f, driftUvDistance = .15f;
    private float speedMultiplier = 1f;


    /// <summary>预出阶段时长，单位秒，0 表示跳过；范围 0–10。只滑出少量纹理，不旋转网格。</summary>
    public float PreparationDuration
    {
        get => preparationDuration;
        set => preparationDuration = ValidateRange(value, 0, 10);
    }
    /// <summary>预出占起点到斩击终点距离的比例，范围 0–1；不是绝对 UV 偏移。推荐 0.05–0.2。</summary>
    public float PreparationUvFraction
    {
        get => preparationUvFraction;
        set => preparationUvFraction = ValidateRange(value, 0, 1);
    }
    /// <summary>快速斩击结束后的慢漂移时长，单位秒，范围 0–10；0 表示不漂移，保持斩击终点。</summary>
    public float DriftDuration
    {
        get => driftDuration;
        set => driftDuration = ValidateRange(value, 0, 10);
    }
    /// <summary>从 AttackUvEnd 继续移动的有符号 UV 距离；正值增加偏移，负值反向，0 关闭。最终位置 = AttackUvEnd + 此值。</summary>
    public float DriftUvDistance
    {
        get => driftUvDistance;
        set => driftUvDistance = ValidateRange(value, -32, 32);
    }

    /// <summary>动画时间倍率；2 表示刀光的预出、斩击、漂移和溶解都以两倍速度推进。</summary>
    public float SpeedMultiplier
    {
        get => speedMultiplier;
        set => speedMultiplier = ValidateRange(value, .01f, 100);
    }
    /// <summary>快速斩击完成的出生后时间，单位秒。</summary>
    public float StrikeEndTime => PreparationDuration + AttackDuration;
    /// <summary>整段 UV 运动的总时长，单位秒；不包含独立的溶解时间。</summary>
    public float UvMotionDuration => StrikeEndTime + (DriftUvDistance != 0 ? DriftDuration : 0);

    /// <summary>入场起始 UV 偏移，范围 -32–32；1 单位等于一帧贴图长度，默认 -1.02。</summary>
    public float AttackUvStart
    {
        get => attackUvStart;
        set => attackUvStart = ValidateRange(value, -32, 32);
    }
    /// <summary>快速斩击段的终点偏移，不是慢漂移的最终位置；0 为完整刀光，负值提前停，正值滑过原位。</summary>
    public float AttackUvEnd
    {
        get => attackUvEnd;
        set => attackUvEnd = ValidateRange(value, -32, 32);
    }
    internal Vector2 AttackUvRange => AutoSweep ? new Vector2(AttackUvStart, AttackUvEnd) : Vector2.Zero;

    /// <summary>溶解进度速率，单位每秒，范围 0–100；达到 1 后保持完全溶解，自动延长寿命至完整溶解。</summary>
    public float DissolveRate
    {
        get => dissolveRate;
        set => dissolveRate = ValidateRange(value, 0, 100);
    }
    /// <summary>从出生到开始溶解的延迟，单位秒，范围 0–10；0 表示预出阶段已经开始溶解。</summary>
    public float DissolveDelay
    {
        get => dissolveDelay;
        set => dissolveDelay = ValidateRange(value, 0, 10);
    }
    /// <summary>预出之后快速斩击段的用时，单位秒，范围 0.001–10；不含预出及慢漂移，越小爆发越快。</summary>
    public float AttackDuration
    {
        get => attackDuration;
        set => attackDuration = ValidateRange(value, .001f, 10);
    }
    /// <summary>溶解边界的软过渡宽度，范围 0.001–0.25；越小越锐利，越大越柔和，不是世界像素宽度。</summary>
    public float DissolveEdgeWidth
    {
        get => dissolveEdgeWidth;
        set => dissolveEdgeWidth = ValidateRange(value, .001f, .25f);
    }
    /// <summary>可见溶解边缘的带宽，使用连续场坐标，范围 0.001–0.5；越大越宽。</summary>
    public float DissolveRimWidth
    {
        get => dissolveRimWidth;
        set => dissolveRimWidth = ValidateRange(value, .001f, .5f);
    }
    /// <summary>溶解边缘颜色；支持大于 1 的 HDR 分量，颜色分量必须非负且有限。</summary>
    public Vector3 DissolveEdgeColor
    {
        get => dissolveEdgeColor;
        set
        {
            if (!IsValidColor(value)) throw new ArgumentOutOfRangeException(nameof(value));
            dissolveEdgeColor = value;
        }
    }
    /// <summary>溶解边缘强度，范围 0–8；0 关闭可见边缘。</summary>
    public float DissolveEdgeIntensity
    {
        get => dissolveEdgeIntensity;
        set => dissolveEdgeIntensity = ValidateRange(value, 0, 8);
    }
    private static float ValidateRange(float value, float min, float max)
    {
        if (!float.IsFinite(value) || value < min || value > max) throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
    private static bool IsValidColor(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) &&
        value.X >= 0 && value.Y >= 0 && value.Z >= 0;

    /// <summary>噪声对定向溶解场的扰动强度，范围 0–1；0 平滑收窄，越大缺口与破碎越强，默认 0.22。</summary>
    public float DissolveNoiseStrength
    {
        get => dissolveNoiseStrength;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            dissolveNoiseStrength = value;
        }
    }

    /// <summary>径向消融方向。方向指向保留时间更长的一侧： (0,-1) 上、(0,1) 下、(-1,0) 左、(1,0) 右。</summary>
    public Vector2 DissolveDirection
    {
        get => dissolveDirection;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.LengthSquared() <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Dissolve direction must be finite and non-zero.");
            dissolveDirection = Vector2.Normalize(value);
        }
    }

    /// <summary>仅按噪声纹理溶解，不叠加由带宽坐标产生的定向推进场。</summary>
    public bool NoiseOnlyDissolve
    {
        get => noiseOnlyDissolve;
        set => noiseOnlyDissolve = value;
    }

    /// <summary>噪声 UV 倍率：X 沿弧长、Y 跨带宽；正有限值，越大纹理越密。不是刀光网格缩放。</summary>
    public Vector2 DissolveUvScale
    {
        get => dissolveUvScale;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X <= 0 || value.Y <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Dissolve UV scale must be finite and positive.");
            dissolveUvScale = value;
        }
    }

    /// <summary>实例世界坐标；修改后改变整个刀光的位置，相机偏移由系统处理。</summary>
    public Vector2 Position
    {
        get => position;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value));
            position = value;
        }
    }

    /// <summary>局部仿射矩阵，包含缩放与三轴旋转（弧度），在世界位置与相机之前应用；UV 运动不会修改它。</summary>
    public Matrix LocalTransform
    {
        get => localTransform;
        set
        {
            if (!IsValidTransform(value)) throw new ArgumentException("Expected a finite affine transform without perspective.", nameof(value));
            localTransform = value;
        }
    }

    /// <summary>手动透明度，限制到 0–1；最终还会乘自动淡出、贴图覆盖度和溶解遮罩。</summary>
    public float Opacity
    {
        get => opacity;
        set
        {
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            opacity = MathHelper.Clamp(value, 0, 1);
        }
    }
    /// <summary>开启整体淡入/末期淡出；关闭后仍会按 AutoDissolve 溶解，且寿命照常结束。</summary>
    public bool AutoFade { get; set; } = true;
    /// <summary>开启预出、爆发、慢漂移三段 UV 运动；关闭时入场偏移为 0，不改变 LocalTransform。</summary>
    public bool AutoSweep { get; set; } = true;
    /// <summary>开启独立计时的溶解；关闭后不再用溶解时长延长寿命。</summary>
    public bool AutoDissolve { get; set; } = true;
    /// <summary>从预出结束开始，沿外圈网格依次发射花瓣，不检查刀光可见性。</summary>
    public bool EmitPetals { get; set; } = true;
    private int petalsEmitted;
    private int petalCount = 28;
    private float petalEmissionDuration = .25f;
    private float petalEndDensity = 2, petalScatterWidth;
    /// <summary>依次发射的花瓣总数，0–128；仍受全局粒子容量限制。</summary>
    public int PetalCount
    {
        get => petalCount;
        set => petalCount = (int)ValidateRange(value, 0, 128);
    }
    /// <summary>沿外圈依次发射全部花瓣的时长，单位秒，范围 0.001–10。</summary>
    public float PetalEmissionDuration
    {
        get => petalEmissionDuration;
        set => petalEmissionDuration = ValidateRange(value, .001f, 10);
    }
    /// <summary>沿 UV 实际运动方向，终点相对起点的密度倍率，1–5；1 均匀，2 表示终点约两倍密。</summary>
    public float PetalEndDensity
    {
        get => petalEndDensity;
        set => petalEndDensity = ValidateRange(value, 1, 5);
    }
    /// <summary>白刃向外撒布的随机宽度，世界像素，0–64；0 紧贴白刃，增大后形成外围带状分布。</summary>
    public float PetalScatterWidth
    {
        get => petalScatterWidth;
        set => petalScatterWidth = ValidateRange(value, 0, 64);
    }
    private float petalTangentialSpeed = 460, petalOutwardSpeed = 240;
    /// <summary>沿 UV 图案实际移动方向的切向速度，世界像素/秒，范围 -1500–1500；负值反向。</summary>
    public float PetalTangentialSpeed
    {
        get => petalTangentialSpeed;
        set => petalTangentialSpeed = ValidateRange(value, -1500, 1500);
    }
    /// <summary>垂直白刃的有符号法向速度，世界像素/秒，范围 -1500–1500；正值向外，负值向内。</summary>
    public float PetalOutwardSpeed
    {
        get => petalOutwardSpeed;
        set => petalOutwardSpeed = ValidateRange(value, -1500, 1500);
    }
    /// <summary>原速动画秒数下的有效寿命；固定倍率时实际用时为 Lifetime / SpeedMultiplier。</summary>
    public float Lifetime => Math.Max(minimumLifetime, Math.Max(Math.Max(AutoSweep ? UvMotionDuration : 0,
        EmitPetals && PetalCount > 0 ? PreparationDuration + PetalEmissionDuration : 0),
        AutoDissolve && DissolveRate > 0 ? DissolveDelay + 1 / DissolveRate : 0));
    /// <summary>按 SpeedMultiplier 缩放后的出生时间，游戏暂停时不推进。</summary>
    public float Age { get; private set; }
    /// <summary>整体寿命进度 0–1；不是独立的斩击或溶解进度。</summary>
    public float Progress => MathHelper.Clamp(Age / Lifetime, 0, 1);
    /// <summary>是否仍由系统更新和绘制；失活后修改参数不会复活。</summary>
    public bool IsActive { get; private set; } = true;
    /// <summary>从 UV 起点到快速斩击终点的进度，包含预出；不包含余势漂移。</summary>
    public float AttackProgress => GetAttackProgress(Age);
    private float GetAttackProgress(float age)
    {
        if (PreparationDuration > 0 && age < PreparationDuration)
            return PreparationUvFraction * (age / PreparationDuration);
        float start = PreparationDuration > 0 ? PreparationUvFraction : 0;
        float t = MathHelper.Clamp((age - PreparationDuration) / AttackDuration, 0, 1);
        return MathHelper.Lerp(start, 1, 1 - MathF.Pow(1 - t, 3));
    }
    /// <summary>兼容入口：启用运动时返回斩击进度，关闭时为 1；完整三段运动请使用 AttackUvOffset。</summary>
    public float Reveal => AutoSweep ? AttackProgress : 1;
    /// <summary>当前实际 UV 偏移，包含预出、斩击及慢漂移；三段连续，最终停在 End + DriftUvDistance。</summary>
    public float AttackUvOffset => GetAttackUvOffset(Age);
    internal float GetAttackUvOffset(float age)
    {
        if (!AutoSweep) return 0;
        float offset = MathHelper.Lerp(AttackUvStart, AttackUvEnd, GetAttackProgress(age));
        if (age < StrikeEndTime || DriftDuration <= 0) return offset;
        float t = MathHelper.Clamp((age - StrikeEndTime) / DriftDuration, 0, 1);
        return offset + DriftUvDistance * (1 - (1 - t) * (1 - t));
    }
    /// <summary>溶解进度，独立于 UV 阶段，按每秒速率累加且不封顶；达到 1 后保持完全溶解。</summary>
    public float Dissolve => AutoDissolve ? Math.Max(0, (Age - DissolveDelay) * DissolveRate) : 0;
    /// <summary>手动透明度乘自动淡入/淡出，不包含 Shader 中的溶解覆盖度。</summary>
    public float DrawOpacity => !IsActive ? 0 : Opacity * (AutoFade
        ? MathHelper.Clamp(Age / (AttackDuration * .3f), 0, 1)
          * (1 - MathHelper.SmoothStep(0, 1, MathHelper.Clamp(((AutoDissolve ? Dissolve : Progress) - .90f) / .10f, 0, 1))) : 1);

    internal SakuraSlashInstance(Vector2 position, Matrix localTransform, float lifetime,
        SakuraSlashPetalBurst petalBurst = null)
    {
        Position = position; LocalTransform = localTransform;
        if (!float.IsFinite(lifetime) || lifetime <= 0 || lifetime > 10) throw new ArgumentOutOfRangeException(nameof(lifetime));
        minimumLifetime = lifetime;
        PetalBurst = petalBurst;
    }

    public void Kill() => IsActive = false;

    internal void Advance(float seconds)
    {
        if (!IsActive) return;
        if (!float.IsFinite(seconds) || seconds <= 0) return;
        Age = Math.Min(Lifetime, Age + seconds * SpeedMultiplier);
        if (Age >= Lifetime) Kill();
    }

    internal bool TakePetalEmission(out int firstIndex, out int count)
    {
        firstIndex = petalsEmitted;
        count = 0;
        if (!IsActive || Age < PreparationDuration || petalsEmitted >= PetalCount) return false;
        if (!EmitPetals)
        {
            petalsEmitted = PetalCount;
            return false;
        }
        float progress = MathHelper.Clamp((Age - PreparationDuration) / PetalEmissionDuration, 0, 1);
        int target = Math.Min(PetalCount, (int)Math.Ceiling(progress * PetalCount));
        count = target - petalsEmitted;
        if (count <= 0) return false;
        petalsEmitted = target;
        return true;
    }

    internal Matrix GetScreenTransform(Vector2 camera, Matrix gameView) => LocalTransform
        * Matrix.CreateTranslation(new Vector3(Position - camera, 0)) * gameView;

    internal static bool IsValidTransform(Matrix m) =>
        float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && m.M14 == 0 &&
        float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && m.M24 == 0 &&
        float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && m.M34 == 0 &&
        float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && m.M44 == 1;
}
