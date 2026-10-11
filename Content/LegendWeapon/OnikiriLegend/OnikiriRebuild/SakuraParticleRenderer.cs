using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraParticleRenderer : IDisposable
{
    private readonly SakuraPetalRenderer renderer;
    private readonly Entry[] order;
    private readonly SakuraBloomRenderer bloom;
    public float FineEmission { get; set; } = 3.25f;
    public float SimpleEmission { get; set; } = 1.25f;
    public int VisibleCount { get; private set; }
    public int DrawCalls => renderer.DrawCalls + (bloom?.DrawCalls ?? 0);
    public int SourceDrawCalls { get; private set; }
    public int SourceTriangleCount { get; private set; }
    public SakuraBloomRenderer Bloom => bloom;
    public int TriangleCount => renderer.TriangleCount + ((bloom?.DrawCalls ?? 0) > 0
        ? SourceTriangleCount + (bloom.DrawCalls - SourceDrawCalls) * 2 : 0);
    private struct Entry { internal int Index; internal float Depth; }
    private static readonly Comparison<Entry> CompareDepth = (a, b) =>
    {
        int result = b.Depth.CompareTo(a.Depth);
        return result != 0 ? result : a.Index.CompareTo(b.Index);
    };

    public SakuraParticleRenderer(GraphicsDevice device, int capacity = SakuraParticleWorld.DefaultCapacity, Effect bloomEffect = null)
    {
        renderer = new SakuraPetalRenderer(device);
        order = new Entry[capacity];
        if (bloomEffect != null) bloom = new SakuraBloomRenderer(device, bloomEffect);
    }

    /// <summary>Camera subtracts world position; transform applies game zoom exactly once.</summary>
    public void Draw(SakuraParticleWorld world, Vector2 camera, Matrix transform, int width, int height, bool bloomEnabled = true)
    {
        DrawGeometry(world, camera, transform, width, height, false);
        if (bloomEnabled) bloom?.Composite(transform);
    }

    public void PrepareBloom(SakuraParticleWorld world, Vector2 camera, int width, int height)
    {
        SourceDrawCalls = SourceTriangleCount = 0;
        bloom?.Prepare(this, world, camera, width, height);
    }

    internal bool HasBloomSource(SakuraParticleWorld world, Vector2 camera, int width, int height, int padding)
    {
        for (int i = 0; i < world.Count; i++)
        {
            ref readonly SakuraPetalParticle p = ref world.GetParticle(i);
            Vector2 point = p.Position - camera;
            float radius = p.Size * PerspectiveScale(p) * 1.5f + padding;
            if (p.Opacity > 0 && point.X + radius >= 0 && point.Y + radius >= 0
                && point.X - radius <= width && point.Y - radius <= height) return true;
        }
        return false;
    }

    internal void DrawBloomSource(SakuraParticleWorld world, Vector2 camera, Matrix transform, int width, int height)
    {
        DrawGeometry(world, camera, transform, width, height, true);
        SourceDrawCalls = renderer.DrawCalls;
        SourceTriangleCount = renderer.TriangleCount;
    }

    private void DrawGeometry(SakuraParticleWorld world, Vector2 camera, Matrix transform, int width, int height, bool emissionOnly)
    {
        VisibleCount = 0;
        float scale = MathF.Max(new Vector2(transform.M11, transform.M12).Length(), new Vector2(transform.M21, transform.M22).Length());
        for (int i = 0; i < world.Count && VisibleCount < order.Length; i++)
        {
            ref readonly SakuraPetalParticle p = ref world.GetParticle(i);
            float perspective = PerspectiveScale(p);
            Vector2 screen = Vector2.Transform(p.Position - camera, transform);
            float radius = p.Size * perspective * scale * 1.5f;
            if (p.Opacity <= 0 || screen.X + radius < 0 || screen.Y + radius < 0
                || screen.X - radius > width || screen.Y - radius > height) continue;
            order[VisibleCount++] = new Entry { Index = i, Depth = p.Depth };
        }
        order.AsSpan(0, VisibleCount).Sort(CompareDepth);
        renderer.Begin(transform);
        try
        {
            for (int i = 0; i < VisibleCount; i++)
            {
                ref readonly SakuraPetalParticle p = ref world.GetParticle(order[i].Index);
                Matrix rotation = Matrix.CreateRotationX(p.Rotation.X) * Matrix.CreateRotationY(p.Rotation.Y)
                    * Matrix.CreateRotationZ(p.Rotation.Z);
                float perspective = PerspectiveScale(p);
                renderer.Add(p.Position - camera, p.Size * perspective, rotation,
                    p.Fine ? SakuraPetalDetail.Fine : SakuraPetalDetail.Simple, p.Opacity,
                    emissionOnly ? (p.Fine ? FineEmission : SimpleEmission) : 0, emissionOnly);
            }
        }
        finally { renderer.End(); }
    }

    public void Dispose() { bloom?.Dispose(); renderer.Dispose(); }

    private static float PerspectiveScale(in SakuraPetalParticle particle)
    {
        if (particle.Perspective <= 0 || particle.Lifetime <= 0) return 1f;
        float progress = MathHelper.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
        return 1f + particle.Perspective * MathHelper.SmoothStep(0f, 1f, progress);
    }
}
