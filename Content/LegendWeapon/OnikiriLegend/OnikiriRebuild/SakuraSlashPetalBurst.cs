using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Cached geometry sampling for particle emission; placement follows the ribbon outer path.</summary>
internal sealed class SakuraSlashPetalBurst
{
    private readonly RibbonMesh mesh;
    private readonly RibbonMaterial material;
    private readonly int columns;
    private readonly Random random;

    internal SakuraSlashPetalBurst(RibbonDefinition definition, int? seed = null)
    {
        mesh = RibbonMeshBuilder.Build(definition); material = definition.Material;
        columns = definition.WidthSegments + 1;
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // Interpolate the same triangle topology and arc-length U used by the renderer.
    internal Vector2 Point(Vector2 uv)
    {
        int row = 0;
        float U(int r) => mesh.Vertices[r * columns * 4 + 2];
        while (row < mesh.CrossSections - 2 && U(row + 1) < uv.X) row++;
        float f = MathHelper.Clamp((uv.X - U(row)) / Math.Max(.000001f, U(row + 1) - U(row)), 0, 1);
        float across = MathHelper.Clamp(uv.Y, 0, 1) * (columns - 1);
        int col = Math.Min((int)across, columns - 2); float v = across - col;
        Vector2 P(int r, int c) { int i = (r * columns + c) * 4; return new(mesh.Vertices[i], mesh.Vertices[i + 1]); }
        Vector2 a = P(row, col), b = P(row + 1, col), c = P(row + 1, col + 1), d = P(row, col + 1);
        return f >= v ? a * (1 - f) + b * (f - v) + c * v : a * (1 - v) + c * f + d * (v - f);
    }

    internal Vector2 Velocity(SakuraSlashInstance slash, Vector2 uv)
    {
        Vector2 Project(Vector2 p) { var v = Vector3.TransformNormal(new Vector3(p, 0), slash.LocalTransform); return new(v.X, v.Y); }
        Vector2 tangent = Project(Point(new(Math.Min(.9999f, uv.X + .002f), uv.Y)) - Point(new(Math.Max(.0001f, uv.X - .002f), uv.Y)));
        Vector2 across = Project(Point(new(uv.X, 0)) - Point(new(uv.X, 1)));
        float len = tangent.Length();
        if (!float.IsFinite(len) || len < .0001f) return Vector2.Zero;
        tangent /= len;
        Vector2 outward = new(-tangent.Y, tangent.X);
        float side = Vector2.Dot(outward, across);
        if (Math.Abs(side) < .0001f) return Vector2.Zero;
        if (side < 0) outward = -outward;
        float strikeTravel = slash.GetAttackUvOffset(slash.StrikeEndTime)
            - slash.GetAttackUvOffset(slash.PreparationDuration)
            + (slash.StrikeEndTime - slash.PreparationDuration) * material.ScrollSpeed;
        // A positive texture-coordinate offset moves the image toward decreasing mesh U.
        float sign = -Math.Sign(strikeTravel);
        return tangent * (sign * slash.PetalTangentialSpeed) + outward * slash.PetalOutwardSpeed;
    }

    internal int Emit(SakuraSlashInstance slash, int firstIndex, int count,
        Action<Vector2, Vector2, SakuraEmissionSettings> emit)
    {
        if (count <= 0 || slash.PetalCount == 0) return 0;
        int emitted = 0;
        float travel = slash.GetAttackUvOffset(slash.StrikeEndTime) - slash.GetAttackUvOffset(slash.PreparationDuration)
            + (slash.StrikeEndTime - slash.PreparationDuration) * material.ScrollSpeed;
        bool denseTowardU0 = travel > 0;
        float density = slash.PetalEndDensity;
        for (int i = firstIndex; i < firstIndex + count; i++)
        {
            float t = (i + .5f) / slash.PetalCount;
            float x = density <= 1.0001f ? t : (MathF.Sqrt(1 + (density * density - 1) * t) - 1) / (density - 1);
            float u = denseTowardU0 ? 1 - x : x;
            u = MathHelper.Clamp(u, .0001f, .9999f);
            var uv = new Vector2(u, 0);
            Vector2 velocity = Velocity(slash, uv);
            float speed = velocity.Length();
            if (!float.IsFinite(speed)) continue;
            Vector2 tangent = Point(new(Math.Min(.9999f, uv.X + .002f), uv.Y))
                - Point(new(Math.Max(.0001f, uv.X - .002f), uv.Y));
            var t3 = Vector3.TransformNormal(new Vector3(tangent, 0), slash.LocalTransform);
            Vector2 tangentScreen = new(t3.X, t3.Y);
            Vector2 tangentDirection = tangentScreen.LengthSquared() > .000001f ? Vector2.Normalize(tangentScreen) : Vector2.UnitX;
            Vector2 outward = new(-tangentDirection.Y, tangentDirection.X);
            var radial = Vector3.TransformNormal(new Vector3(Point(new(uv.X, 0)) - Point(new(uv.X, 1)), 0), slash.LocalTransform);
            if (Vector2.Dot(outward, new Vector2(radial.X, radial.Y)) < 0) outward = -outward;
            var p = Vector3.Transform(new Vector3(Point(uv), 0), slash.LocalTransform);
            Vector2 scatter = outward * (random.NextSingle() * slash.PetalScatterWidth);
            bool fine = i % 4 == 0;
            Vector2 direction = speed > .0001f ? velocity / speed : outward;
            emit(slash.Position + new Vector2(p.X, p.Y) + scatter, direction, new SakuraEmissionSettings
            {
                FineCount = fine ? 1 : 0, SimpleCount = fine ? 0 : 1,
                Speed = speed * (.8f + random.NextSingle() * .4f), Spread = .12f, SpawnRadius = 0,
                Scale = 1, Lifetime = 0.95f + random.NextSingle() * .3f, Drag = 2.1f, Gravity = 38, Curl = 80
            });
            emitted++;
        }
        return emitted;
    }
}
