using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>本地冲击锥网格；与原二次贝塞尔轮廓、UV 滚动及非预乘混合一致，无外部绘制助手。</summary>
public sealed class SakuraDashWindRenderer : IDisposable
{
    private const int Pairs = 100;
    private readonly GraphicsDevice device;
    private readonly BasicEffect effect;
    private readonly Texture2D dark, color;
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[Pairs * 2];
    private readonly short[] indices = new short[(Pairs - 1) * 6];

    public SakuraDashWindRenderer(GraphicsDevice device, Texture2D dark, Texture2D color)
    {
        this.device = device;
        this.dark = dark;
        this.color = color;
        effect = new BasicEffect(device)
        {
            TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false,
            World = Matrix.Identity, View = Matrix.Identity
        };
        for (int i = 0; i < Pairs - 1; i++)
        {
            int v = i * 2, j = i * 6;
            indices[j] = (short)v; indices[j + 1] = (short)(v + 2); indices[j + 2] = (short)(v + 1);
            indices[j + 3] = (short)(v + 1); indices[j + 4] = (short)(v + 2); indices[j + 5] = (short)(v + 3);
        }
    }

    /// <summary>调用前结束 SpriteBatch；固定数组重复使用，不逐帧创建网格或请求资源。</summary>
    public void Draw(Vector2 origin, Vector2 direction, float age, float distance, Matrix worldToScreen)
    {
        float remaining = SakuraDashTiming.GetLineTimeLeft(age);
        if (age < 0f || remaining <= 0f || distance <= 0f) return;
        float progress = remaining / SakuraDashTiming.DashLineFrames;
        float traveled = distance * SakuraDashTiming.GetDashProgress(age);
        Vector2 start = direction, end = -direction * traveled;
        Vector2 control = start + (end - start) * .01f;
        Vector2 normal = new(direction.Y, -direction.X);
        Vector2 anchor = origin + direction * traveled;
        float controlWidth = 120f * progress;
        float texScale = MathHelper.Lerp(.25f, 1f, progress);
        for (int i = 0; i < Pairs; i++)
        {
            // 保留原来 200 点采样的端点/纹理映射，不改变参考轮廓。
            float t = i * 2f / (Pairs * 2 - 1), s = 1f - t;
            Vector2 center = anchor + start * (s * s) + control * (2f * s * t) + end * (t * t);
            Vector2 halfWidth = normal * (2f * s * t * controlWidth);
            float u = .5f + (.5f - i / (float)Pairs) / texScale + age % 1200f / 65f;
            vertices[i * 2] = new(new Vector3(Vector2.Transform(center + halfWidth, worldToScreen), 0f), Color.Black, new Vector2(u, 1f));
            vertices[i * 2 + 1] = new(new Vector3(Vector2.Transform(center - halfWidth, worldToScreen), 0f), Color.Black, new Vector2(u, 0f));
        }
        var oldBlend = device.BlendState;
        var oldDepth = device.DepthStencilState;
        var oldRaster = device.RasterizerState;
        var oldSampler = device.SamplerStates[0];
        var oldTexture = device.Textures[0];
        try
        {
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1);
            Layer(dark);
            for (int i = 0; i < Pairs; i++)
            {
                Color tint = Color.Lerp(new Color(255, 0, 0), new Color(255, 50, 150), i / (float)Pairs);
                vertices[i * 2].Color = vertices[i * 2 + 1].Color = tint;
            }
            Layer(color);
        }
        finally
        {
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRaster;
            device.SamplerStates[0] = oldSampler;
            device.Textures[0] = oldTexture;
        }
    }

    private void Layer(Texture2D texture)
    {
        effect.Texture = texture;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.SamplerStates[0] = SamplerState.LinearWrap;
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, indices.Length / 3);
        }
    }

    public void Dispose() => effect.Dispose();
}
