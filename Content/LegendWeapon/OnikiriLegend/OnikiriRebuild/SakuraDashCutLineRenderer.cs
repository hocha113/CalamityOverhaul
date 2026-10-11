using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>黑红暗底 + 白芯粉边斩线，各层共用本地 FuzzyEdge 侵蚀出断点。</summary>
public sealed class SakuraDashCutLineRenderer : IDisposable
{
    public const string EffectPath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/FuzzyEdge";
    public const string TexturePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/ShotLine";
    public const string NoisePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/voronoiOrigin";
    public const string DarkTexturePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/Eff_Trail_518_02";
    private const int DarkSegments = 48;
    private readonly GraphicsDevice device;
    private readonly SpriteBatch batch;
    private readonly Effect effect;
    private readonly Texture2D texture;
    private readonly Texture2D noise;
    private readonly Texture2D darkTexture;
    private readonly BasicEffect darkVertexEffect;
    private readonly VertexPositionColorTexture[] darkVertices = new VertexPositionColorTexture[(DarkSegments + 1) * 2];
    private readonly short[] darkIndices = new short[DarkSegments * 6];
    public Vector2 LocalAnchor { get; }
    public float LocalRotation { get; }

    public SakuraDashCutLineRenderer(GraphicsDevice device, RibbonDefinition definition,
        Texture2D texture, Texture2D noise, Effect fuzzyEdge, Texture2D darkTexture)
    {
        this.device = device;
        this.texture = texture;
        this.noise = noise;
        this.darkTexture = darkTexture;
        // 不修改资源缓存的共享 Effect 参数，不拥有 ModContent 提供的贴图。
        effect = fuzzyEdge.Clone();
        batch = new SpriteBatch(device);
        darkVertexEffect = new BasicEffect(device)
        {
            TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false,
            View = Matrix.Identity, Texture = darkTexture
        };
        for (int i = 0; i < DarkSegments; i++)
        {
            int v = i * 2, index = i * 6;
            darkIndices[index] = (short)v;
            darkIndices[index + 1] = (short)(v + 1);
            darkIndices[index + 2] = (short)(v + 2);
            darkIndices[index + 3] = (short)(v + 1);
            darkIndices[index + 4] = (short)(v + 3);
            darkIndices[index + 5] = (short)(v + 2);
        }
        (LocalAnchor, LocalRotation) = FindOuterTip(definition);
    }

    private static (Vector2, float) FindOuterTip(RibbonDefinition definition)
    {
        Vector2 tip = Vector2.Zero;
        Vector2 tangent = Vector2.UnitY;
        float furthest = float.NegativeInfinity;
        foreach (var segment in definition.Segments)
        {
            var c = segment.Outer;
            Vector2 a = new(c[0].X, c[0].Y), b = new(c[1].X, c[1].Y);
            Vector2 d = new(c[2].X, c[2].Y), e = new(c[3].X, c[3].Y);
            for (int i = 0; i <= 128; i++)
            {
                float t = i / 128f, s = 1f - t;
                Vector2 p = a * (s * s * s) + b * (3f * s * s * t)
                    + d * (3f * s * t * t) + e * (t * t * t);
                if (p.X <= furthest) continue;
                furthest = p.X;
                tip = p;
                tangent = (b - a) * (3f * s * s) + (d - b) * (6f * s * t) + (e - d) * (3f * t * t);
            }
        }
        return (tip, tangent.LengthSquared() > .001f ? MathF.Atan2(tangent.Y, tangent.X) : MathHelper.PiOver2);
    }

    public static float GetErosion(float slashAge)
    {
        // 0.4~0.6 是原 FuzzyEdge 开始切断中心线的区间；留足帧数展示碎段。
        if (slashAge <= 6f) return 0f;
        if (slashAge < 10f) return MathHelper.Lerp(0f, .43f, (slashAge - 6f) / 4f);
        return MathHelper.Lerp(.43f, .61f, MathHelper.Clamp((slashAge - 10f) / 10f, 0f, 1f));
    }

    /// <summary>在无活动 SpriteBatch 时调用；slashAge 是瞬斩自身的帧龄。</summary>
    public void Draw(Vector2 endpoint, float slashRotation, float slashAge, Matrix worldToScreen,
        float scale = 1f, float length = 380f, float width = 42f, float angleOffset = 0f,
        float darkWidthScale = 1f, float darkOpacity = 1f, bool mirrorX = false, bool mirrorY = false)
    {
        if (slashAge < 0f || slashAge >= SakuraDashTiming.SlashLifetimeFrames || scale <= 0f || length <= 0f || width <= 0f)
            return;
        Vector2 anchor = new(mirrorX ? -LocalAnchor.X : LocalAnchor.X, mirrorY ? -LocalAnchor.Y : LocalAnchor.Y);
        Vector2 center = endpoint + Vector2.Transform(anchor * scale, Matrix.CreateRotationZ(slashRotation));
        float localAngle = LocalRotation + angleOffset;
        float rotation = slashRotation + MathF.Atan2(MathF.Sin(localAngle) * (mirrorY ? -1f : 1f),
            MathF.Cos(localAngle) * (mirrorX ? -1f : 1f));
        float expand = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((slashAge + 1f) / 3f, 0f, 1f));
        float lineLength = length * MathHelper.Lerp(.4f, 1f, expand) * scale;
        float lineWidth = width * scale;
        float opacity = 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((slashAge - 17f) / 3f, 0f, 1f));
        float flash = MathHelper.Lerp(1.5f, 1f, MathHelper.Clamp(slashAge / 5f, 0f, 1f));

        var oldBlend = device.BlendState;
        var oldDepth = device.DepthStencilState;
        var oldRaster = device.RasterizerState;
        var oldSampler0 = device.SamplerStates[0];
        var oldSampler1 = device.SamplerStates[1];
        var oldTexture0 = device.Textures[0];
        var oldTexture1 = device.Textures[1];
        try
        {
            effect.Parameters["Intensity"].SetValue(GetErosion(slashAge));
            effect.Parameters["NoiseScale"].SetValue(new Vector2(1f, .5f));
            // 固定噪声相位：碎段稳定消蚀，不在每一帧随机闪烁。
            effect.Parameters["NoiseOffset"].SetValue(new Vector2(.4f, 0f));
            effect.Parameters["UseErosion"].SetValue(true);
            effect.Parameters["Symmetric"].SetValue(false);
            if (darkWidthScale > 0f && darkOpacity > 0f)
            {
                // 独立纹理暗底：sin 收尖网格，先黑后红，不能用 Additive 画黑色。
                float darkWidth = lineWidth * darkWidthScale
                    * MathHelper.Lerp(1.25f, .85f, MathHelper.Clamp(slashAge / 10f, 0f, 1f));
                float alpha = opacity * MathHelper.Clamp(darkOpacity, 0f, 1f);
                DarkLayer(darkWidth * 1.6f, new Vector4(.1f, 0f, 0f, alpha));
                DarkLayer(darkWidth * 1.15f, new Vector4(1f, .025f, .07f, alpha * .95f));
            }
            batch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, worldToScreen);
            try
            {
                Layer(lineWidth * 2.4f, new Vector3(1f, .18f, .42f), .38f);
                Layer(lineWidth, new Vector3(1f, .4f, .63f), 1.25f);
                Layer(lineWidth * .3f, new Vector3(1f, .94f, .98f), 2.1f);
            }
            finally { batch.End(); }
        }
        finally
        {
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRaster;
            device.SamplerStates[0] = oldSampler0;
            device.SamplerStates[1] = oldSampler1;
            device.Textures[0] = oldTexture0;
            device.Textures[1] = oldTexture1;
        }

        void DarkLayer(float layerWidth, Vector4 tint)
        {
            for (int i = 0; i <= DarkSegments; i++)
            {
                float u = i / (float)DarkSegments;
                float halfWidth = MathF.Sin(MathHelper.Pi * u) * layerWidth * .5f;
                float x = (u - .5f) * lineLength;
                darkVertices[i * 2] = new VertexPositionColorTexture(new Vector3(x, -halfWidth, 0f), Color.White, new Vector2(u, 0f));
                darkVertices[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(x, halfWidth, 0f), Color.White, new Vector2(u, 1f));
            }
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            darkVertexEffect.World = Matrix.CreateRotationZ(rotation)
                * Matrix.CreateTranslation(center.X, center.Y, 0f) * worldToScreen;
            darkVertexEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1);
            // BasicEffect 提供顶点变换，原 FuzzyEdge（仅像素 Shader）提供与光晕相同的侵蚀。
            darkVertexEffect.CurrentTechnique.Passes[0].Apply();
            effect.Parameters["ImageColor"].SetValue(tint);
            effect.CurrentTechnique.Passes[0].Apply();
            device.Textures[0] = darkTexture;
            device.Textures[1] = noise;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, darkVertices, 0, darkVertices.Length,
                darkIndices, 0, darkIndices.Length / 3);
        }

        void Layer(float layerWidth, Vector3 color, float intensity)
        {
            effect.Parameters["ImageColor"].SetValue(new Vector4(color * (intensity * flash), opacity));
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            batch.Draw(texture, center, null, Color.White, rotation,
                new Vector2(texture.Width, texture.Height) * .5f,
                new Vector2(lineLength / texture.Width, layerWidth / texture.Height), SpriteEffects.None, 0f);
        }
    }

    public void Dispose()
    {
        batch.Dispose();
        darkVertexEffect.Dispose();
        effect.Dispose();
    }
}
