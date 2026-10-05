using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Projects a rotating, open-ended air arc into the plane perpendicular to dash motion.</summary>
public sealed class SakuraDashRingRenderer : IDisposable
{
    private const int Segments = 48;
    private const int RingLifetime = 20;
    private const float DefaultOpeningAngle = .7f;
    // The former +.12 UV offset on two texture repeats moved features toward decreasing phase.
    private const float DefaultRotationSpeed = -.12f * (MathHelper.TwoPi - DefaultOpeningAngle) / 2f;
    public const int TotalLifetime = RingLifetime;

    private readonly BlendState PremultipliedAdditive = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One
    };

    private readonly BasicEffect effect;
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[(Segments + 1) * 2];
    private readonly short[] indices = new short[Segments * 6];

    public SakuraDashRingRenderer(GraphicsDevice device)
    {
        effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            World = Matrix.Identity,
            View = Matrix.Identity
        };
        for (int i = 0; i < Segments; i++)
        {
            int vertex = i * 2;
            int index = i * 6;
            indices[index] = (short)vertex;
            indices[index + 1] = (short)(vertex + 1);
            indices[index + 2] = (short)(vertex + 2);
            indices[index + 3] = (short)(vertex + 1);
            indices[index + 4] = (short)(vertex + 3);
            indices[index + 5] = (short)(vertex + 2);
        }
    }

    public void Draw(GraphicsDevice device, Texture2D texture, Vector2 origin, Vector2 direction,
        float age, float dashSpeed, Matrix worldToScreen,
        float startAngle = 0f, float rotationSpeed = DefaultRotationSpeed,
        float radiusStart = 63f, float radiusEnd = 116f,
        float depthStart = 26f, float depthEnd = 58f,
        float thicknessStart = 32f, float thicknessEnd = 55f,
        float openingAngle = DefaultOpeningAngle, float tipFraction = .16f)
    {
        if (texture == null || direction.LengthSquared() < .01f) return;
        openingAngle = MathHelper.Clamp(openingAngle, 0f, MathHelper.TwoPi - .01f);
        tipFraction = MathHelper.Clamp(tipFraction, .001f, .5f);
        direction.Normalize();
        Vector2 transverse = new(-direction.Y, direction.X);
        effect.Texture = texture;
        effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width,
            device.Viewport.Height, 0, 0, 1);

        BlendState oldBlend = device.BlendState;
        DepthStencilState oldDepth = device.DepthStencilState;
        RasterizerState oldRasterizer = device.RasterizerState;
        SamplerState oldSampler = device.SamplerStates[0];
        Texture oldTexture = device.Textures[0];
        try
        {
            device.BlendState = PremultipliedAdditive;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            if (age >= 0 && age < RingLifetime)
            {
                float rotationAngle = age * rotationSpeed;
                float progress = age / RingLifetime;
                float entrance = MathHelper.Clamp(age / 2f, 0f, 1f);
                float fade = MathF.Pow(1f - progress, .9f) * entrance;
                float radius = MathHelper.Lerp(radiusStart, radiusEnd, MathF.Sqrt(progress));
                float depth = MathHelper.Lerp(depthStart, depthEnd, progress);
                float thickness = MathHelper.Lerp(thicknessStart, thicknessEnd, progress);
                Vector2 center = origin - direction * (22f + age * MathHelper.Min(dashSpeed * .22f, 4.8f));
                // Start with the gap at the bottom, then carry it around the fixed ellipse.
                float bottomAngle = MathF.Atan2(transverse.Y * radius, direction.Y * depth);
                for (int i = 0; i <= Segments; i++)
                {
                    float phase = i / (float)Segments;
                    float angle = bottomAngle + openingAngle * .5f + phase * (MathHelper.TwoPi - openingAngle)
                        + startAngle + rotationAngle;
                    float sine = MathF.Sin(angle);
                    float cosine = MathF.Cos(angle);
                    float tip = MathHelper.SmoothStep(0f, 1f,
                        MathHelper.Clamp(MathF.Min(phase, 1f - phase) / tipFraction, 0f, 1f));
                    Vector2 radial = transverse * sine + direction * cosine;
                    Vector2 point = center + transverse * (sine * radius) + direction * (cosine * depth);
                    float front = .55f + .45f * MathHelper.Clamp(cosine, 0f, 1f);
                    float brightness = fade * front * tip;
                    Color tint = Color.Lerp(new Color(239, 173, 222), Color.White, MathHelper.Clamp(cosine, 0f, 1f) * .65f);
                    tint *= brightness;
                    float u = phase * 2f;
                    Vector2 outer = Vector2.Transform(point + radial * (thickness * tip * .5f), worldToScreen);
                    Vector2 inner = Vector2.Transform(point - radial * (thickness * tip * .5f), worldToScreen);
                    vertices[i * 2] = new VertexPositionColorTexture(new Vector3(outer, 0f), tint, new Vector2(u, 0f));
                    vertices[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(inner, 0f), tint, new Vector2(u, 1f));
                }

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.Textures[0] = texture;
                    device.SamplerStates[0] = SamplerState.LinearWrap;
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length,
                        indices, 0, indices.Length / 3);
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

    public void Dispose()
    {
        effect.Dispose();
        PremultipliedAdditive.Dispose();
    }
}
