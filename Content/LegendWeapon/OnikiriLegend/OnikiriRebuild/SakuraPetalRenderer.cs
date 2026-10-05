using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public enum SakuraPetalDetail { Reference, Fine, Simple }

/// <summary>Shared meshes/material/buffers. Call Begin/Add/End without an active SpriteBatch.</summary>
public sealed class SakuraPetalRenderer : IDisposable
{
    private static readonly SakuraPetalMesh ReferenceMesh = new(64, 6);
    private static readonly SakuraPetalMesh FineMesh = new(24, 2);
    private static readonly SakuraPetalMesh SimpleMesh = new(12, 1);
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-.45f, -.65f, -1));
    private readonly SakuraHdrVertex[] projected = new SakuraHdrVertex[385];
    private readonly float[] depths = new float[385];
    private readonly TriangleOrder[] triangles = new TriangleOrder[704];
    private readonly SakuraHdrVertex[] vertices = new SakuraHdrVertex[24576];
    private readonly BasicEffect effect;
    private readonly GraphicsDevice device;
    private BlendState savedBlend;
    private DepthStencilState savedDepth;
    private RasterizerState savedRasterizer;
    private bool begun;
    private int vertexCount;
    public int DrawCalls { get; private set; }
    public int TriangleCount { get; private set; }

    private struct TriangleOrder { internal int Index; internal float Depth; }
    private static readonly Comparison<TriangleOrder> CompareTriangles = (a, b) => b.Depth.CompareTo(a.Depth);

    public SakuraPetalRenderer(GraphicsDevice device)
    {
        this.device = device;
        effect = new BasicEffect(device) { VertexColorEnabled = true, TextureEnabled = false, LightingEnabled = false };
    }

    public void Begin(Matrix? viewTransform = null)
    {
        if (begun) throw new InvalidOperationException("End the previous petal batch first.");
        begun = true;
        vertexCount = DrawCalls = TriangleCount = 0;
        savedBlend = device.BlendState; savedDepth = device.DepthStencilState; savedRasterizer = device.RasterizerState;
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        effect.World = viewTransform ?? Matrix.Identity;
        effect.View = Matrix.Identity;
        effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, -1, 1);
    }

    public void Add(Vector2 position, float size, Matrix rotation, SakuraPetalDetail detail, float opacity = 1, float emission = 0,
        bool emissionOnly = false)
    {
        if (!begun) throw new InvalidOperationException("Begin the petal batch first.");
        if (size <= 0 || opacity <= 0) return;
        opacity = MathHelper.Clamp(opacity, 0, 1);
        emission = float.IsFinite(emission) ? MathHelper.Clamp(emission, 0, 4) : 0;
        SakuraPetalMesh mesh = detail == SakuraPetalDetail.Reference ? ReferenceMesh
            : detail == SakuraPetalDetail.Fine ? FineMesh : SimpleMesh;
        for (int i = 0; i < mesh.Positions.Length; i++)
        {
            Vector3 p = Vector3.Transform(mesh.Positions[i], rotation);
            Vector3 normal = Vector3.TransformNormal(mesh.Normals[i], rotation);
            bool back = normal.Z > 0;
            if (back) normal = -normal;
            float diffuse = MathF.Max(0, Vector3.Dot(normal, Light));
            Vector3 color = (back ? mesh.BackColors[i] : mesh.FrontColors[i]) * (.86f + .14f * diffuse);
            float fold = mesh.Fold[i] * diffuse;
            color += new Vector3(fold, fold * .8f, fold * .7f);
            Vector3 radiance = new Vector3(1f, .28f, .46f) * emission * (.85f + .15f * diffuse);
            color = emissionOnly ? radiance : color + radiance;
            float perspective = 4.5f / (4.5f + p.Z);
            projected[i] = new SakuraHdrVertex(new Vector3(position + new Vector2(p.X, p.Y) * size * perspective, 0),
                new Vector4(color * opacity, opacity));
            depths[i] = p.Z;
        }
        int count = mesh.Indices.Length / 3;
        for (int i = 0; i < count; i++)
        {
            int t = i * 3;
            triangles[i] = new TriangleOrder { Index = t,
                Depth = depths[mesh.Indices[t]] + depths[mesh.Indices[t + 1]] + depths[mesh.Indices[t + 2]] };
        }
        triangles.AsSpan(0, count).Sort(CompareTriangles);
        if (vertexCount + mesh.Indices.Length > vertices.Length) Flush();
        for (int i = 0; i < count; i++)
        {
            int t = triangles[i].Index;
            vertices[vertexCount++] = projected[mesh.Indices[t]];
            vertices[vertexCount++] = projected[mesh.Indices[t + 1]];
            vertices[vertexCount++] = projected[mesh.Indices[t + 2]];
        }
        TriangleCount += count;
    }

    private void Flush()
    {
        if (vertexCount == 0) return;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount / 3);
            DrawCalls++;
        }
        vertexCount = 0;
    }

    public void End()
    {
        if (!begun) return;
        try { Flush(); }
        finally
        {
            device.BlendState = savedBlend; device.DepthStencilState = savedDepth; device.RasterizerState = savedRasterizer;
            begun = false;
        }
    }

    // Preserve the single-petal reference preview API.
    public void Draw(Vector2 position, float size, Matrix rotation, float opacity = 1, Matrix? viewTransform = null)
    {
        Begin(viewTransform);
        try { Add(position, size, rotation, SakuraPetalDetail.Reference, opacity); }
        finally { End(); }
    }

    public void Dispose() => effect.Dispose();
}
