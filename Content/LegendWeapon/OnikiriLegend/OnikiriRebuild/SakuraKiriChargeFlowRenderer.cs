using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraKiriChargeFlowRenderer : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly BasicEffect effect;
    private VertexPositionColorTexture[] vertices = [];
    private int[] indices = [];

    internal SakuraKiriChargeFlowRenderer(GraphicsDevice device)
    {
        this.device = device;
        effect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true,
            LightingEnabled = false };
    }

    internal void Draw(Texture2D texture, Texture2D coreTexture, Vector2 target,
        int facing, int gravity, float seconds, float progress, bool foreground,
        SakuraKiriFlowSettings settings, float chargeSeconds, Matrix worldToScreen)
    {
        if (texture == null || settings.Radius <= 0f || settings.Width <= 0f ||
            settings.Brightness <= 0f || settings.Speed <= 0f) return;

        int segments = Math.Clamp(settings.Segments, 4, 256);
        EnsureBuffers(segments);
        BlendState oldBlend = device.BlendState;
        DepthStencilState oldDepth = device.DepthStencilState;
        RasterizerState oldRasterizer = device.RasterizerState;
        SamplerState oldSampler = device.SamplerStates[0];
        Texture oldTexture = device.Textures[0];
        try
        {
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.PointClamp;
            effect.World = worldToScreen;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width,
                device.Viewport.Height, 0, -1, 1);

            for (int stream = foreground ? 4 : 0; stream < (foreground ? 8 : 4); stream++)
            {
                float strength = SakuraKiriChargeFlow.StreamStrength(stream, progress);
                if (strength <= 0f) continue;
                float startTime = SakuraKiriChargeFlow.StreamStartProgress(stream) * chargeSeconds;
                float travel = Math.Max(0f, seconds - startTime) * settings.Speed;
                float head = SakuraKiriChargeFlow.Fract(travel);
                SakuraKiriChargeFlow.GetCurve(target, facing, gravity, stream, settings,
                    out Vector2 p0, out Vector2 p1, out Vector2 p2, out Vector2 p3);
                if (travel >= 1f) DrawPulse(head + 1f);
                DrawPulse(head);

                void DrawPulse(float pulseHead)
                {
                    float tail = pulseHead - .47f;
                    float start = Math.Max(0f, tail);
                    float end = Math.Min(1f, pulseHead);
                    if (end <= start) return;
                    int count = Math.Max(2, (int)MathF.Ceiling((end - start) * segments));
                    FillStrip(p0, p1, p2, p3, start, end, tail, count, strength,
                        foreground, settings, false);
                    DrawStrip(texture, count);
                    FillStrip(p0, p1, p2, p3, start, end, tail, count, strength,
                        foreground, settings, true);
                    DrawStrip(coreTexture, count);
                }
            }
        }
        finally
        {
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRasterizer;
            device.SamplerStates[0] = oldSampler;
            device.Textures[0] = oldTexture;
        }
    }

    private void FillStrip(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float start, float head, float tail, int count, float strength,
        bool foreground, SakuraKiriFlowSettings settings, bool core)
    {
        float energy = strength * settings.Brightness;
        Color hue = core ? (foreground ? new Color(222, 20, 20) : new Color(122, 20, 20))
            : (foreground ? new Color(22, 20, 20) : new Color(50, 20, 20));
        for (int i = 0; i <= count; i++)
        {
            float t = MathHelper.Lerp(start, head, i / (float)count);
            Vector2 position = SakuraKiriChargeFlow.Bezier(p0, p1, p2, p3, t);
            Vector2 tangent = SakuraKiriChargeFlow.Tangent(p0, p1, p2, p3, t);
            Vector2 normal = tangent.LengthSquared() > .0001f
                ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X)) : Vector2.UnitY;
            float phase = MathHelper.Clamp((t - tail) / .47f, 0f, 1f);
            float pulse = MathF.Sin(MathHelper.Pi * phase);
            float taper = MathF.Sin(MathHelper.Pi * (.06f + t * .88f));
            float width = core ? (foreground ? 2.2f : 1.3f)
                : settings.Width * (.42f + .58f * taper) * (.35f + .65f * pulse);
            float alpha = MathHelper.Clamp(energy * pulse * (foreground ? .75f : .36f), 0f, 1f);
            if (core) alpha *= foreground ? .85f : .95f;
            Color color = hue * alpha;
            float u = phase;
            vertices[i * 2] = new VertexPositionColorTexture(
                new Vector3(position - normal * width * .5f, 0f), color, new Vector2(u, 0f));
            vertices[i * 2 + 1] = new VertexPositionColorTexture(
                new Vector3(position + normal * width * .5f, 0f), color, new Vector2(u, 1f));
        }
    }

    private void DrawStrip(Texture2D texture, int count)
    {
        effect.Texture = texture;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0,
                (count + 1) * 2, indices, 0, count * 2);
        }
    }

    private void EnsureBuffers(int segments)
    {
        if (vertices.Length >= (segments + 1) * 2) return;
        vertices = new VertexPositionColorTexture[(segments + 1) * 2];
        indices = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            int vertex = i * 2;
            int index = i * 6;
            indices[index] = vertex;
            indices[index + 1] = vertex + 1;
            indices[index + 2] = vertex + 2;
            indices[index + 3] = vertex + 1;
            indices[index + 4] = vertex + 3;
            indices[index + 5] = vertex + 2;
        }
    }

    public void Dispose() => effect.Dispose();
}
