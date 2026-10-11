using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public struct SakuraPetalParticle
{
    public Vector2 Position, Velocity, FlowNormal;
    public Vector3 Rotation, AngularVelocity;
    public float Size, Age, Lifetime, Drag, Gravity, Curl, Phase, Depth, Perspective;
    public bool Fine;
    public readonly float Opacity => MathHelper.SmoothStep(0, 1, MathHelper.Clamp(Age / .06f, 0, 1))
        * MathHelper.SmoothStep(0, 1, MathHelper.Clamp((Lifetime - Age) / .65f, 0, 1));
}

/// <summary>Fixed storage, no Terraria/GPU dependencies. Clients simulate visuals locally.</summary>
public sealed class SakuraParticleWorld
{
    public const int DefaultCapacity = 384;
    public const int DefaultFineCapacity = 48;
    private readonly SakuraPetalParticle[] particles;
    private readonly int fineCapacity;
    private uint randomState;
    public int Count { get; private set; }
    public int FineCount { get; private set; }
    public int SimpleCount => Count - FineCount;
    public int Capacity => particles.Length;
    public int DroppedCount { get; private set; }

    public SakuraParticleWorld(int capacity = DefaultCapacity, int maxFine = DefaultFineCapacity, uint seed = 12345)
    {
        if (capacity < 1 || capacity > 4096 || maxFine < 0 || maxFine > capacity) throw new ArgumentOutOfRangeException(nameof(capacity));
        particles = new SakuraPetalParticle[capacity];
        fineCapacity = maxFine;
        randomState = seed == 0 ? 12345u : seed;
    }

    public ref readonly SakuraPetalParticle GetParticle(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        return ref particles[index];
    }

    public void Clear() { Count = FineCount = DroppedCount = 0; }

    private float Random(float min, float max)
    {
        randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
        return min + (max - min) * ((randomState >> 8) / 16777216f);
    }

    internal static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    private static float Safe(float value, float fallback, float min, float max)
        => float.IsFinite(value) ? MathHelper.Clamp(value, min, max) : fallback;

    /// <returns>The number actually accepted. Overflow is dropped, never queued.</returns>
    public int EmitBurst(Vector2 origin, Vector2 direction, SakuraEmissionSettings settings)
    {
        if (!Finite(origin) || !Finite(direction)) return 0;
        float magnitude = MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
        direction = magnitude < .0001f ? Vector2.UnitX : Vector2.Normalize(direction / magnitude);
        Vector2 normal = new(-direction.Y, direction.X);
        float speed = Safe(settings.Speed, 620, 0, 3000), spread = Safe(settings.Spread, .48f, 0, MathHelper.Pi);
        float radius = Safe(settings.SpawnRadius, 18, 0, 512), scale = Safe(settings.Scale, 1, .1f, 4);
        float lifetime = Safe(settings.Lifetime, 2.8f, .1f, 10), drag = Safe(settings.Drag, 1.15f, 0, 20);
        float gravity = Safe(settings.Gravity, 58, -500, 500), curl = Safe(settings.Curl, 125, 0, 1000);
        float perspectiveStrength = Safe(settings.PerspectiveStrength, 0, 0, 10);
        int added = 0;
        int fine = Math.Clamp(settings.FineCount, 0, Capacity), simple = Math.Clamp(settings.SimpleCount, 0, Capacity);
        for (int i = 0; i < fine + simple; i++)
        {
            bool detailed = i < fine;
            if (Count == Capacity || (detailed && FineCount == fineCapacity)) { DroppedCount++; continue; }
            float depth = detailed ? Random(0, .35f) : Random(.3f, 1);
            float sizeScale = 1f;
            Vector2 spawnOffset;
            Vector2 travel;
            if (settings.SphericalBurst)
            {
                // 用随机球面方向的 XY 投影形成球形扩散；Z 只参与尺寸差异，不参与深度移动。
                float z = Random(-1f, 1f);
                float planar = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
                float azimuth = Random(-MathHelper.Pi, MathHelper.Pi);
                Vector3 sphereDirection = new(
                    MathF.Cos(azimuth) * planar,
                    MathF.Sin(azimuth) * planar,
                    z);
                spawnOffset = new Vector2(sphereDirection.X, sphereDirection.Y) * radius;
                travel = new Vector2(sphereDirection.X, sphereDirection.Y);
                if (travel.LengthSquared() < .0001f) travel = Vector2.UnitX;
                travel.Normalize();
                depth = detailed ? .08f + (sphereDirection.Z + 1f) * .16f
                    : .28f + (sphereDirection.Z + 1f) * .36f;
                sizeScale = .8f + (sphereDirection.Z + 1f) * .1f;
            }
            else
            {
                float angle = Random(-spread, spread);
                travel = direction * MathF.Cos(angle) + normal * MathF.Sin(angle);
                spawnOffset = normal * Random(-radius, radius)
                    + direction * Random(-radius * .5f, radius * .5f);
            }
            particles[Count++] = new SakuraPetalParticle
            {
                Position = origin + spawnOffset,
                Velocity = travel * speed * Random(.58f, 1.12f), FlowNormal = normal,
                Perspective = settings.SphericalBurst ? perspectiveStrength : 0,
                Rotation = new Vector3(Random(-.65f, .65f), Random(0, MathHelper.TwoPi), Random(-MathHelper.Pi, MathHelper.Pi)),
                AngularVelocity = new Vector3(Random(-2, 2), Random(2, 5.5f) * (Random(0, 1) < .5f ? -1 : 1), Random(-2.5f, 2.5f)),
                // Scale 1 now matches the original emitter at Scale 0.3.
                Size = (detailed ? Random(25, 43) : Random(7, 17)) * .3f * scale * sizeScale,
                Lifetime = lifetime * Random(detailed ? .85f : .65f, 1.15f),
                Drag = drag, Gravity = gravity, Curl = curl, Phase = Random(0, MathHelper.TwoPi),
                Depth = depth, Fine = detailed
            };
            if (detailed) FineCount++;
            added++;
        }
        return added;
    }

    public void Update(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0) return;
        float remaining = MathF.Min(seconds, .25f);
        while (remaining > .000001f)
        {
            float dt = MathF.Min(remaining, 1f / 60);
            Step(dt); remaining -= dt;
        }
    }

    private void Step(float dt)
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            ref SakuraPetalParticle p = ref particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                if (p.Fine) FineCount--;
                particles[i] = particles[--Count]; continue;
            }
            float curl = MathF.Sin(p.Age * 6 + p.Phase) * p.Curl * MathF.Exp(-p.Age * 1.2f);
            p.Velocity += (p.FlowNormal * curl + new Vector2(0, p.Gravity)) * dt;
            p.Velocity *= MathF.Exp(-p.Drag * dt);
            p.Position += p.Velocity * dt;
            p.Rotation += p.AngularVelocity * dt;
        }
    }
}
