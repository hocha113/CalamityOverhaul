using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Coarse outer-rim fan shared by the damage broad phase and precise collision.</summary>
internal sealed class SakuraSlashHitArc
{
    private const int Samples = 33;
    private const int GrazePixels = 14;
    private readonly Vector2[] outer;

    internal SakuraSlashHitArc(RibbonDefinition definition)
    {
        RibbonMesh mesh = RibbonMeshBuilder.Build(definition);
        int stride = (definition.WidthSegments + 1) * 4;
        outer = new Vector2[Math.Min(Samples, mesh.CrossSections)];
        for (int i = 0; i < outer.Length; i++)
        {
            int row = i * (mesh.CrossSections - 1) / (outer.Length - 1);
            outer[i] = new Vector2(mesh.Vertices[row * stride], mesh.Vertices[row * stride + 1]);
        }
    }

    internal Rectangle Bounds(SakuraWeaponPose pose, float scale)
    {
        Vector2 center = pose.TransformLocalPoint(Vector2.Zero);
        Matrix transform = pose.CreateSlashTransform(scale, .1f);
        float left = center.X, right = center.X, top = center.Y, bottom = center.Y;
        foreach (Vector2 point in outer)
        {
            Vector2 world = WorldPoint(center, transform, point);
            left = MathF.Min(left, world.X);
            right = MathF.Max(right, world.X);
            top = MathF.Min(top, world.Y);
            bottom = MathF.Max(bottom, world.Y);
        }
        int x = (int)MathF.Floor(left) - GrazePixels;
        int y = (int)MathF.Floor(top) - GrazePixels;
        return new Rectangle(x, y, (int)MathF.Ceiling(right) + GrazePixels - x,
            (int)MathF.Ceiling(bottom) + GrazePixels - y);
    }

    internal bool Intersects(Rectangle target, SakuraWeaponPose pose, float scale)
    {
        target.Inflate(GrazePixels, GrazePixels);
        Vector2 center = pose.TransformLocalPoint(Vector2.Zero);
        Matrix transform = pose.CreateSlashTransform(scale, .1f);
        Vector2 previous = WorldPoint(center, transform, outer[0]);
        for (int i = 1; i < outer.Length; i++)
        {
            Vector2 next = WorldPoint(center, transform, outer[i]);
            if (TriangleIntersectsBox(center, previous, next, target)) return true;
            previous = next;
        }
        return false;
    }

    private static Vector2 WorldPoint(Vector2 center, Matrix transform, Vector2 point)
    {
        Vector3 offset = Vector3.TransformNormal(new Vector3(point, 0f), transform);
        return center + new Vector2(offset.X, offset.Y);
    }

    private static bool TriangleIntersectsBox(Vector2 a, Vector2 b, Vector2 c, Rectangle box)
    {
        float left = box.Left, right = box.Right, top = box.Top, bottom = box.Bottom;
        if (MathF.Max(a.X, MathF.Max(b.X, c.X)) < left || MathF.Min(a.X, MathF.Min(b.X, c.X)) > right ||
            MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) < top || MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) > bottom) return false;

        Vector2 boxCenter = new((left + right) * .5f, (top + bottom) * .5f);
        Vector2 half = new((right - left) * .5f, (bottom - top) * .5f);
        return OverlapsEdgeAxis(a, b, c, boxCenter, half) &&
            OverlapsEdgeAxis(b, c, a, boxCenter, half) &&
            OverlapsEdgeAxis(c, a, b, boxCenter, half);
    }

    private static bool OverlapsEdgeAxis(Vector2 a, Vector2 b, Vector2 c, Vector2 boxCenter, Vector2 half)
    {
        Vector2 edge = b - a;
        Vector2 axis = new(-edge.Y, edge.X);
        float center = Vector2.Dot(boxCenter, axis);
        float radius = half.X * MathF.Abs(axis.X) + half.Y * MathF.Abs(axis.Y);
        float pa = Vector2.Dot(a, axis), pb = Vector2.Dot(b, axis), pc = Vector2.Dot(c, axis);
        return MathF.Min(pa, MathF.Min(pb, pc)) <= center + radius &&
            MathF.Max(pa, MathF.Max(pb, pc)) >= center - radius;
    }
}
