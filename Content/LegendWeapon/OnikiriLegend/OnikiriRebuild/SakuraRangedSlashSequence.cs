using System;
using System.Collections.Generic;
using System.Linq;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>一刀的手工时序和空间姿态；Rotation 为 XYZ 旋转矩阵，时间单位为原速秒。</summary>
public sealed record SakuraRangedSlashCut
{
    public float StartDelay { get; init; }
    public Matrix Rotation { get; init; } = Matrix.Identity;
    public Vector2 Offset { get; init; }
    public Vector2 Scale { get; init; } = Vector2.One;
}

/// <summary>与游戏环境无关的手工乱斩时间轴；绘制和碰撞共享 XYZ 旋转后的正交投影。</summary>
internal sealed class SakuraRangedSlashSequence
{
    public const float DefaultSpeed = 2f;
    public const float SlashScale = 1f;
    public float Duration { get; }
    public SakuraSlashInstance[] Slashes { get; }
    private readonly SakuraRangedSlashCut[] cuts;
    private readonly RibbonMesh mesh;
    private readonly SakuraSlashPetalBurst petals;
    private readonly Vector2[][] hitVertices;
    private readonly Vector2[] hitMin, hitMax;
    private readonly HashSet<int>[] hitNpcs;
    private readonly List<int> damageSlashes = new();
    private float age;
    public Vector2 LocalPivot { get; }
    public IReadOnlyList<int> DamageSlashes => damageSlashes;
    public bool CanDamage => damageSlashes.Count > 0;
    public bool IsComplete => age >= Duration;

    public SakuraRangedSlashSequence(RibbonMesh mesh, IEnumerable<SakuraRangedSlashCut> cuts,
        SakuraSlashPetalBurst petals = null)
    {
        this.mesh = mesh;
        this.petals = petals;
        // OrderBy is stable: simultaneous cuts retain their authored draw order.
        this.cuts = cuts.Select(c => c ?? throw new ArgumentException("A slash cut cannot be null.", nameof(cuts)))
            .OrderBy(c => c.StartDelay).ToArray();
        foreach (var cut in this.cuts)
        {
            Matrix r = cut.Rotation;
            if (!float.IsFinite(cut.StartDelay) || cut.StartDelay < 0 ||
                !float.IsFinite(cut.Scale.X) || !float.IsFinite(cut.Scale.Y) ||
                cut.Scale.X <= 0 || cut.Scale.Y <= 0 ||
                !float.IsFinite(cut.Offset.X) || !float.IsFinite(cut.Offset.Y) ||
                !float.IsFinite(r.M11) || !float.IsFinite(r.M12) || !float.IsFinite(r.M13) ||
                !float.IsFinite(r.M21) || !float.IsFinite(r.M22) || !float.IsFinite(r.M23) ||
                !float.IsFinite(r.M31) || !float.IsFinite(r.M32) || !float.IsFinite(r.M33) ||
                r.M14 != 0 || r.M24 != 0 || r.M34 != 0 ||
                r.M41 != 0 || r.M42 != 0 || r.M43 != 0 || r.M44 != 1)
                throw new ArgumentException("Cuts require finite nonnegative delays, positive scales, finite offsets and XYZ rotation matrices.", nameof(cuts));
        }
        int count = this.cuts.Length;
        Slashes = new SakuraSlashInstance[count];
        hitVertices = new Vector2[count][];
        hitMin = new Vector2[count];
        hitMax = new Vector2[count];
        hitNpcs = new HashSet<int>[count];
        for (int i = 0; i < count; i++)
        {
            hitVertices[i] = new Vector2[mesh.Vertices.Length / 4];
            hitNpcs[i] = new HashSet<int>();
        }
        Duration = count == 0 ? 0f : this.cuts[^1].StartDelay + SakuraSlashStyle.Lifetime;
        LocalPivot = GetRibbonCenter(mesh);
    }

    public float StartTime(int index) => cuts[index].StartDelay;
    public Matrix Transform(int index, float rotation, float scale) =>
        Matrix.CreateTranslation(new Vector3(-LocalPivot, 0f))
        * Matrix.CreateScale(cuts[index].Scale.X, cuts[index].Scale.Y, 1f)
        * cuts[index].Rotation
        * Matrix.CreateTranslation(new Vector3(cuts[index].Offset, 0f))
        * Matrix.CreateScale(scale) * Matrix.CreateRotationZ(rotation);

    // The authored origin is beside the crescent. Anchor the middle of the actual
    // ribbon to Projectile.Center before applying each authored pose and offset.
    private static Vector2 GetRibbonCenter(RibbonMesh mesh)
    {
        int columns = mesh.Vertices.Length / (mesh.CrossSections * 4);
        int row = 0;
        float U(int r) => mesh.Vertices[r * columns * 4 + 2];
        while (row < mesh.CrossSections - 2 && U(row + 1) < .5f) row++;
        float fraction = (.5f - U(row)) / Math.Max(.000001f, U(row + 1) - U(row));
        Vector2 P(int r, int c)
        {
            int v = (r * columns + c) * 4;
            return new Vector2(mesh.Vertices[v], mesh.Vertices[v + 1]);
        }
        Vector2 outer = Vector2.Lerp(P(row, 0), P(row + 1, 0), fraction);
        Vector2 inner = Vector2.Lerp(P(row, columns - 1), P(row + 1, columns - 1), fraction);
        return (outer + inner) * .5f;
    }

    // Absolute base-time allows a late network client to reconstruct surviving slashes.
    public void AdvanceTo(float nextAge, Vector2 position, float rotation, float scale, float speed)
    {
        float previousAge = age;
        age = Math.Max(age, nextAge);
        damageSlashes.Clear();
        for (int i = 0; i < cuts.Length; i++)
        {
            float start = StartTime(i);
            if (age < start) break;
            if (Slashes[i] == null)
            {
                Slashes[i] = new SakuraSlashInstance(position, Transform(i, rotation, scale),
                    SakuraSlashStyle.Lifetime, petals);
                SakuraSlashStyle.Apply(Slashes[i], speed);
            }
            SakuraSlashInstance slash = Slashes[i];
            slash.SpeedMultiplier = speed;
            slash.Position = position;
            slash.LocalTransform = Transform(i, rotation, scale);
            // Advance only the part of this tick after this slash's scheduled birth.
            slash.Advance(Math.Max(0, age - start - slash.Age) / speed);
            if (SakuraPlayback.CrossesWindow(previousAge, age, start + SakuraSlashStyle.PreparationDuration,
                start + SakuraSlashStyle.PreparationDuration + SakuraSlashStyle.AttackDuration))
            {
                damageSlashes.Add(i);
                UpdateHitVertices(i);
            }
        }
    }

    private void UpdateHitVertices(int index)
    {
        SakuraSlashInstance slash = Slashes[index];
        hitMin[index] = new Vector2(float.MaxValue);
        hitMax[index] = new Vector2(float.MinValue);
        for (int i = 0; i < hitVertices[index].Length; i++)
        {
            Vector2 p = Vector2.Transform(new Vector2(mesh.Vertices[i * 4], mesh.Vertices[i * 4 + 1]),
                slash.LocalTransform) + slash.Position;
            hitVertices[index][i] = p;
            hitMin[index] = Vector2.Min(hitMin[index], p);
            hitMax[index] = Vector2.Max(hitMax[index], p);
        }
    }

    // SAT against the actual JSON triangles. Fading older slashes cannot deal another hit.
    public bool Collides(Rectangle target)
    {
        foreach (int index in damageSlashes)
            if (Collides(index, target)) return true;
        return false;
    }

    public bool CanHitNpc(int npc, Rectangle target)
    {
        foreach (int index in damageSlashes)
            if (!hitNpcs[index].Contains(npc) && Collides(index, target)) return true;
        return false;
    }

    // Terraria applies one hit per NPC per projectile tick. Coincident cuts merge that
    // hit, but all their intersecting windows are consumed so they cannot hit again.
    public void RegisterNpcHit(int npc, Rectangle target)
    {
        foreach (int index in damageSlashes)
            if (Collides(index, target)) hitNpcs[index].Add(npc);
    }

    private bool Collides(int index, Rectangle target)
    {
        if (target.Right < hitMin[index].X || target.Left > hitMax[index].X ||
            target.Bottom < hitMin[index].Y || target.Top > hitMax[index].Y) return false;
        Vector2[] vertices = hitVertices[index];
        Vector2 center = new(target.X + target.Width * .5f, target.Y + target.Height * .5f);
        Vector2 half = new(target.Width * .5f, target.Height * .5f);
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            Vector2 a = vertices[mesh.Indices[i]], b = vertices[mesh.Indices[i + 1]],
                c = vertices[mesh.Indices[i + 2]];
            Vector2 ab = b - a, ac = c - a, bc = c - b;
            if (Math.Abs(ab.X * ac.Y - ab.Y * ac.X) < .0001f) continue;
            if (Overlaps(Vector2.UnitX, a, b, c, center, half) &&
                Overlaps(Vector2.UnitY, a, b, c, center, half) &&
                Overlaps(new Vector2(-ab.Y, ab.X), a, b, c, center, half) &&
                Overlaps(new Vector2(-ac.Y, ac.X), a, b, c, center, half) &&
                Overlaps(new Vector2(-bc.Y, bc.X), a, b, c, center, half)) return true;
        }
        return false;
    }

    private static bool Overlaps(Vector2 axis, Vector2 a, Vector2 b, Vector2 c, Vector2 center, Vector2 half)
    {
        float x = Vector2.Dot(a, axis), y = Vector2.Dot(b, axis), z = Vector2.Dot(c, axis);
        float middle = Vector2.Dot(center, axis);
        float radius = Math.Abs(axis.X) * half.X + Math.Abs(axis.Y) * half.Y;
        return Math.Min(x, Math.Min(y, z)) <= middle + radius &&
            Math.Max(x, Math.Max(y, z)) >= middle - radius;
    }

    public void Kill()
    {
        foreach (SakuraSlashInstance slash in Slashes) slash?.Kill();
        damageSlashes.Clear();
    }
}
