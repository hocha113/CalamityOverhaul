using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vector2 = System.Numerics.Vector2;
using System.Text.Json;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;

// Mod-owned snapshot of the portable core; isolated from the editor's SlashStudio types.
public sealed record Point2(float X, float Y)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Vector2 Vector => new(X, Y);
    public static Point2 From(Vector2 v) => new(v.X, v.Y);
}

public sealed class RibbonSegment
{
    public Point2[] Outer { get; set; } = [];
    public Point2[] Inner { get; set; } = [];
}

public sealed class RibbonMaterial
{
    // Texture is a logical asset name; consumers resolve it themselves.
    public string Texture { get; set; } = "silk.png";
    public float[] Tint { get; set; } = [1, .72f, .86f];
    public float Opacity { get; set; } = 1;
    public float Repeat { get; set; } = 1;
    public float Offset { get; set; }
    public float ScrollSpeed { get; set; } = .18f;
    public bool Wrap { get; set; } = true;
    public string Blend { get; set; } = "alpha";
    // Atlas contains animation frames. FrameAxis is the direction of the blade inside one frame.
    public int AtlasColumns { get; set; } = 1;
    public int AtlasRows { get; set; } = 1;
    public int FrameCount { get; set; } = 1;
    public int FrameIndex { get; set; }
    public float FrameRate { get; set; }
    public string FrameOrder { get; set; } = "row-major";
    public string FrameAxis { get; set; } = "horizontal";
    public bool FlipAlong { get; set; }
    public bool FlipAcross { get; set; }

    public int GetFrameIndex(float time)
    {
        if (FrameRate <= 0) return FrameIndex;
        if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        return (FrameIndex + (int)(Math.Max(0, Math.Floor((double)time * FrameRate + 1e-6)) % FrameCount)) % FrameCount;
    }

    public Point2 GetAtlasCell(float time)
    {
        int frame = GetFrameIndex(time);
        return FrameOrder == "column-major"
            ? new Point2(frame / AtlasRows, frame % AtlasRows)
            : new Point2(frame % AtlasColumns, frame / AtlasColumns);
    }

    public Vector2 MapTexture(Vector2 uv, float time)
    {
        var cell = GetAtlasCell(time);
        if (FlipAlong) uv.X = 1 - uv.X;
        if (FlipAcross) uv.Y = 1 - uv.Y;
        Vector2 local = FrameAxis == "vertical" ? new Vector2(uv.Y, uv.X) : uv;
        return new Vector2((cell.X + local.X) / AtlasColumns, (cell.Y + local.Y) / AtlasRows);
    }
}

public sealed class RibbonDefinition
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "crescent";
    public RibbonSegment[] Segments { get; set; } = [];
    public float Tolerance { get; set; } = 1;
    public int WidthSegments { get; set; } = 4;
    public RibbonMaterial Material { get; set; } = new();
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    public static RibbonDefinition Parse(string json)
    {
        var definition = JsonSerializer.Deserialize<RibbonDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException("预设为空。");
        definition.Validate();
        return definition;
    }
    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, JsonOptions); }
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("不支持此预设版本。");
        if (Segments is not { Length: >= 1 and <= 32 }) throw new InvalidDataException("需要 1–32 段曲线。");
        if (!float.IsFinite(Tolerance) || Tolerance < .05f || Tolerance > 20 || WidthSegments < 1 || WidthSegments > 24)
            throw new InvalidDataException("细分参数超出范围。");
        foreach (var s in Segments)
        {
            if (s == null || s.Outer is not { Length: 4 } || s.Inner is not { Length: 4 })
                throw new InvalidDataException("每段内外沿均需 4 个控制点。");
            foreach (var p in s.Outer.Concat(s.Inner))
                if (p == null || !float.IsFinite(p.X) || !float.IsFinite(p.Y) || Math.Abs(p.X) > 100000 || Math.Abs(p.Y) > 100000)
                    throw new InvalidDataException("控制点坐标无效。");
        }
        for (int i = 1; i < Segments.Length; i++)
            if (Vector2.Distance(Segments[i - 1].Outer[3].Vector, Segments[i].Outer[0].Vector) > .01f ||
                Vector2.Distance(Segments[i - 1].Inner[3].Vector, Segments[i].Inner[0].Vector) > .01f)
                throw new InvalidDataException("相邻曲线段的端点必须连接。");
        var m = Material;
        if (m == null || m.Tint is not { Length: 3 } || m.Tint.Any(v => !float.IsFinite(v) || v < 0 || v > 1) ||
            !float.IsFinite(m.Opacity) || m.Opacity < 0 || m.Opacity > 1 || !float.IsFinite(m.Repeat) || m.Repeat < .05f || m.Repeat > 32 ||
            !float.IsFinite(m.Offset) || Math.Abs(m.Offset) > 10000 || !float.IsFinite(m.ScrollSpeed) || Math.Abs(m.ScrollSpeed) > 10 ||
            m.Blend is not ("alpha" or "additive") || string.IsNullOrWhiteSpace(m.Texture) || m.Texture.Length > 256 ||
            m.AtlasColumns is < 1 or > 64 || m.AtlasRows is < 1 or > 64 || m.FrameCount is < 1 or > 4096 ||
            m.FrameCount > m.AtlasColumns * m.AtlasRows || m.FrameIndex < 0 || m.FrameIndex >= m.FrameCount ||
            !float.IsFinite(m.FrameRate) || m.FrameRate < 0 || m.FrameRate > 120 ||
            m.FrameOrder is not ("row-major" or "column-major") || m.FrameAxis is not ("horizontal" or "vertical"))
            throw new InvalidDataException("材质参数无效。");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128) throw new InvalidDataException("名称需为 1–128 个字符。");
    }
}

public sealed class RibbonMesh
{
    // Interleaved x, y, u, v. V=0 outer edge; V=1 inner edge. Y points down.
    public float[] Vertices { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public string[] Warnings { get; init; } = [];
    public int CrossSections { get; init; }
}

public static class RibbonMeshBuilder
{
    public static Vector2 Evaluate(Point2[] p, float t)
    {
        float q = 1 - t;
        return q * q * q * p[0].Vector + 3 * q * q * t * p[1].Vector +
            3 * q * t * t * p[2].Vector + t * t * t * p[3].Vector;
    }

    public static RibbonMesh Build(RibbonDefinition definition)
    {
        definition.Validate();
        var rows = new List<(Vector2 Outer, Vector2 Inner)>();
        var warnings = new List<string>();
        bool limited = false;
        int maxRows = Math.Min(2048, 60000 / (definition.WidthSegments + 1));
        void Add(RibbonSegment s, float t)
        {
            if (rows.Count >= maxRows) throw new InvalidDataException("曲线过于复杂；请提高细分容差或减少曲线段。");
            rows.Add((Evaluate(s.Outer, t), Evaluate(s.Inner, t)));
        }
        void Sample(RibbonSegment s, float a, float b, int depth)
        {
            var oa = Evaluate(s.Outer, a); var ob = Evaluate(s.Outer, b);
            var ia = Evaluate(s.Inner, a); var ib = Evaluate(s.Inner, b);
            float error = 0;
            // Quarter points also detect S bends whose midpoint lies on the chord.
            for (int j = 1; j <= 3; j++)
            {
                float f = j * .25f, t = a + (b - a) * f;
                error = Math.Max(error, Vector2.Distance(Evaluate(s.Outer, t), Vector2.Lerp(oa, ob, f)));
                error = Math.Max(error, Vector2.Distance(Evaluate(s.Inner, t), Vector2.Lerp(ia, ib, f)));
            }
            if ((depth < 3 || error > definition.Tolerance) && depth < 10)
            { float mid = (a + b) * .5f; Sample(s, a, mid, depth + 1); Sample(s, mid, b, depth + 1); }
            else { if (error > definition.Tolerance) limited = true; Add(s, b); }
        }
        Add(definition.Segments[0], 0);
        foreach (var segment in definition.Segments) Sample(segment, 0, 1, 0);
        var distances = new float[rows.Count];
        for (int i = 1; i < rows.Count; i++)
            distances[i] = distances[i - 1] + .5f * (Vector2.Distance(rows[i].Outer, rows[i - 1].Outer) + Vector2.Distance(rows[i].Inner, rows[i - 1].Inner));
        float length = distances[^1];
        if (length < .0001f) throw new InvalidDataException("刀光长度为零。");
        int columns = definition.WidthSegments + 1;
        var vertices = new float[rows.Count * columns * 4];
        for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < columns; j++)
            {
                float v = j / (float)(columns - 1);
                var p = Vector2.Lerp(rows[i].Outer, rows[i].Inner, v);
                int at = (i * columns + j) * 4;
                vertices[at] = p.X; vertices[at + 1] = p.Y; vertices[at + 2] = distances[i] / length; vertices[at + 3] = v;
            }
        var indices = new List<int>();
        int winding = 0, flipped = 0;
        void Triangle(int a, int b, int c)
        {
            var va = new Vector2(vertices[a * 4], vertices[a * 4 + 1]);
            var vb = new Vector2(vertices[b * 4], vertices[b * 4 + 1]);
            var vc = new Vector2(vertices[c * 4], vertices[c * 4 + 1]);
            float area = Cross(vb - va, vc - va);
            if (Math.Abs(area) < .00001f) return; // Closed tips do not emit zero-area triangles.
            if (winding == 0) winding = Math.Sign(area);
            else if (winding != Math.Sign(area)) flipped++;
            indices.Add(a); indices.Add(b); indices.Add(c);
        }
        for (int i = 0; i < rows.Count - 1; i++)
            for (int j = 0; j < columns - 1; j++)
            {
                int a = i * columns + j, b = a + columns;
                Triangle(a, b, b + 1); Triangle(a, b + 1, a + 1);
            }
        if (indices.Count == 0) throw new InvalidDataException("刀光没有可绘制面积。");
        if (flipped > 0) warnings.Add($"检测到 {flipped} 个翻转三角形；请调整手柄或拆分曲线。");
        // Non-local boundary intersections can overlap even without a local winding flip.
        var outline = rows.Select(r => r.Outer).Concat(rows.AsEnumerable().Reverse().Select(r => r.Inner)).ToArray();
        bool intersects = false;
        for (int i = 0; i < outline.Length && !intersects; i++)
            for (int j = i + 2; j < outline.Length; j++)
            {
                if (i == 0 && j == outline.Length - 1) continue;
                if (Intersects(outline[i], outline[(i + 1) % outline.Length], outline[j], outline[(j + 1) % outline.Length])) { intersects = true; break; }
            }
        if (intersects) warnings.Add("边界存在交叉；重叠区域可能出现亮度堆积。");
        if (limited) warnings.Add("部分曲线已达到细分上限。");
        return new RibbonMesh { Vertices = vertices, Indices = indices.ToArray(), CrossSections = rows.Count, Warnings = warnings.ToArray() };
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X) ||
            Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y)) return false;
        return Cross(b - a, c - a) * Cross(b - a, d - a) < -.0001f && Cross(d - c, a - c) * Cross(d - c, b - c) < -.0001f;
    }
}
