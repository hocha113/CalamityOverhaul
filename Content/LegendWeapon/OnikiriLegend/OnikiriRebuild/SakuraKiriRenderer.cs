using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraKiriLayer
{
    public float Width { get; set; }
    public float LengthScale { get; set; }
    public Vector4 Tint { get; set; }
    public Vector2 TextureScale { get; set; } = Vector2.One;
    public Vector2 FlowSpeed { get; set; }

    public SakuraKiriLayer(float width, float lengthScale, Vector4 tint, Vector2 flowSpeed)
    {
        Width = width;
        LengthScale = lengthScale;
        Tint = tint;
        FlowSpeed = flowSpeed;
    }

    public static SakuraKiriLayer CreateBlack() =>
        new(67f, 1.025f, new Vector4(0.1f, 0.0f, 0.0f, 1f), new Vector2(1.82f, 0f));

    public static SakuraKiriLayer CreateRed() =>
        new(48f, 1.012f, new Vector4(1f, .025f, .07f, .95f), new Vector2(1.82f, 0f));

    public static SakuraKiriLayer CreateWhite() =>
        new(25f, 1f, Vector4.One, new Vector2(1.82f, 0f));
}

public sealed class SakuraKiriLine
{
    /// <summary>是否使用大横斩的镜头冲击倍率；实际倍率在弹幕的表达式属性中调整。</summary>
    public bool HeavyCameraPunch { get; set; }
    /// <summary>是否绘制这根斩切线。</summary>
    public bool Visible { get; set; } = true;
    /// <summary>是否叠加 Projectile.rotation；false 时使用 Rotation 的绝对旋转。</summary>
    public bool FollowProjectileRotation { get; set; } = true;
    /// <summary>三维旋转矩阵，可组合 X、Y、Z 轴旋转。</summary>
    public Matrix Rotation { get; set; } = Matrix.Identity;
    /// <summary>总长度倍率。</summary>
    public float LengthScale { get; set; } = 1f;
    /// <summary>整根斩击的宽度倍率。</summary>
    public float WidthScale { get; set; } = 1f;
    /// <summary>透视焦距；数值越大，纵深缩放越平。</summary>
    public float FocalLength { get; set; } = 1100f;
    /// <summary>屏幕坐标偏移，单位为像素。</summary>
    public Vector2 Offset { get; set; } = Vector2.Zero;
    /// <summary>出现延迟，单位为秒。</summary>
    public float StartDelay { get; set; }

    /// <summary>延长阶段的激光颜色，A 通道为基础透明度。</summary>
    public Vector4 FlashColor { get; set; } = new(1f, .12f, .25f, .85f);
    /// <summary>峰值后持续存在的粉色光晕颜色，A 通道为基础透明度。</summary>
    public Vector4 GlowColor { get; set; } = new(1f, .12f, .25f, .85f);

    /// <summary>从左端延长到完整长度的时间，单位为秒。</summary>
    public float LengthGrowTime { get; set; } = .16f;
    /// <summary>初始细光宽度，相对白芯宽度的倍率。</summary>
    public float GlowInitialWidthScale { get; set; } = .9f;
    /// <summary>爆发峰值宽度倍率。</summary>
    public float GlowPeakWidthScale { get; set; } = 5f;
    /// <summary>最终细线的光晕宽度倍率。</summary>
    public float GlowHoldWidthScale { get; set; } = .9f;
    /// <summary>从细光上升到峰值的时间，单位为秒。</summary>
    public float GlowExpandTime { get; set; } = .09f;
    /// <summary>从峰值衰减到长尾状态的时间，单位为秒。</summary>
    public float GlowDuration { get; set; } = .44f;
    /// <summary>长尾粉色光晕的亮度倍率。</summary>
    public float GlowHoldIntensity { get; set; } = .95f;

    /// <summary>爆发上升曲线幂值；小于 1 会更快到达峰值。</summary>
    public float GlowAttackPower { get; set; } = .82f;
    /// <summary>峰值后的指数衰减速率；越大则前段下降越陡、尾部越短。</summary>
    public float GlowDecayRate { get; set; } = 5.5f;

    /// <summary>爆发开始后光晕收缩完成的时间，单位为秒。</summary>
    public float LineNarrowTime { get; set; } = .5f;
    /// <summary>暗底单独收缩完成的时间，从延长结束开始计时；可小于 LineNarrowTime。</summary>
    public float DarkNarrowTime { get; set; } = 1.6f;
    /// <summary>峰值暗底宽度相对峰值光晕的倍率，1 表示黑底与光晕等宽。</summary>
    public float DarkPeakWidthScale { get; set; } = 1.5f;
    /// <summary>暗底透明度倍率；0 表示完全隐藏。</summary>
    public float DarkOpacityScale { get; set; } = 1f;
    /// <summary>外部粉色光晕相对当前光带的宽度倍率。</summary>
    public float GlowHaloWidthScale { get; set; } = 1.4f;
    /// <summary>外部粉色光晕的强度倍率。</summary>
    public float GlowHaloIntensity { get; set; } = .32f;
    /// <summary>外部光晕边缘衰减幂值；越小越宽、越柔和。</summary>
    public float GlowHaloPower { get; set; } = .85f;
    /// <summary>粉色光带边缘衰减幂值；越大越集中在中间。</summary>
    public float GlowEdgePower { get; set; } = 1.7f;
    /// <summary>白色核心边缘衰减幂值；越大白芯越细。</summary>
    public float GlowCorePower { get; set; } = 2.4f;

    /// <summary>收尾阶段光晕膨胀的峰值宽度倍率。</summary>
    public float FinaleGlowPeakWidthScale { get; set; } = 5f;
    /// <summary>收尾阶段光晕的峰值亮度倍率。</summary>
    public float FinaleGlowIntensity { get; set; } = 2.6f;
}

public sealed class SakuraKiriRenderer
{
    private const string TechniqueName = "SakuraKiri";
    private VertexPositionTexture[] vertices;
    private int[] indices;

    // 在 SpriteBatch.End 与 SpriteBatch.Begin 之间调用；时间和流动速度以秒计。
    public void Draw(GraphicsDevice device, Effect effect, Texture2D texture, Vector2 center, Matrix rotation,
        Matrix worldToScreen, float time, float length, int segments,
        SakuraKiriLayer black, SakuraKiriLayer red, SakuraKiriLayer white,
        float focalLength, float widthScale = 1f, SakuraKiriLine line = null,
        float finaleTime = -1f, float finaleDelay = 0f,
        float finaleExpandTime = .09f, float finaleShrinkTime = .14f)
    {
        if (device == null || effect == null || texture == null
            || length <= 0f || widthScale <= 0f) return;
        float age = line == null ? time : time - line.StartDelay;
        if (age <= 0f) return;
        segments = Math.Clamp(segments, 2, 512);
        EnsureMeshBuffers(segments);
        focalLength = Math.Max(1f, focalLength);

        EffectTechnique technique = effect.Techniques[TechniqueName];
        EffectParameter transform = effect.Parameters["Transform"];
        if (technique == null || transform == null) return;

        transform.SetValue(worldToScreen
            * Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1));
        effect.CurrentTechnique = technique;

        BlendState oldBlend = device.BlendState;
        DepthStencilState oldDepth = device.DepthStencilState;
        RasterizerState oldRasterizer = device.RasterizerState;
        SamplerState oldSampler = device.SamplerStates[0];
        Texture oldTexture = device.Textures[0];
        try
        {
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            float localFinaleTime = finaleTime - Math.Max(0f, finaleDelay);
            float finaleExpand = Math.Max(.001f, finaleExpandTime);
            float finaleShrink = Math.Max(.001f, finaleShrinkTime);
            float finaleDuration = finaleExpand + finaleShrink;
            bool inFinale = line != null && localFinaleTime >= 0f;
            if (inFinale && localFinaleTime >= finaleDuration) return;

            float lengthProgress = line == null ? 1f :
                Ease(MathHelper.Clamp(age / Math.Max(.001f, line.LengthGrowTime), 0f, 1f));
            float burstAge = line == null ? age : age - Math.Max(0f, line.LengthGrowTime);
            float initialWidth = line?.GlowInitialWidthScale ?? .35f;
            float holdWidth = line?.GlowHoldWidthScale ?? initialWidth;
            float peakWidth = line?.GlowPeakWidthScale ?? 5f;
            float glowWidth = initialWidth;
            float darkWidth = 0f;
            float darkOpacity = 0f;
            float glowIntensity = line?.GlowHoldIntensity ?? .95f;
            float coreIntensity = .85f;
            Vector4 glowColor = line?.FlashColor ?? new Vector4(1f, .12f, .55f, .85f);
            if (burstAge > 0f && line != null)
            {
                float expandTime = Math.Max(.001f, line.GlowExpandTime);
                float attack = SakuraBurstCurve.Attack(burstAge / expandTime, line.GlowAttackPower);
                float contractionTime = Math.Max(expandTime + .001f, line.LineNarrowTime);
                float contraction = Ease(MathHelper.Clamp((burstAge - expandTime) /
                    (contractionTime - expandTime), 0f, 1f));
                float darkContractionTime = Math.Max(expandTime + .001f, line.DarkNarrowTime);
                float darkContraction = Ease(MathHelper.Clamp((burstAge - expandTime) /
                    (darkContractionTime - expandTime), 0f, 1f));
                float brightnessEnd = Math.Max(expandTime + .001f, line.GlowDuration);
                float brightness = burstAge < expandTime ? attack :
                    SakuraBurstCurve.Decay((burstAge - expandTime) / (brightnessEnd - expandTime), line.GlowDecayRate);
                float widthBurst = burstAge < expandTime ? attack : 1f - contraction;

                glowWidth = MathHelper.Lerp(burstAge < expandTime ? initialWidth : holdWidth,
                    peakWidth, widthBurst);
                darkWidth = white.Width * peakWidth / Math.Max(1f, black.Width)
                    * Math.Max(0f, line.DarkPeakWidthScale) * widthBurst * (1f - darkContraction);
                darkOpacity = MathHelper.Clamp(widthBurst * (1f - darkContraction)
                    * line.DarkOpacityScale, 0f, 1f);
                glowIntensity = MathHelper.Lerp(line.GlowHoldIntensity, 2.6f, brightness);
                coreIntensity = MathHelper.Lerp(.85f, 2.5f, brightness);
                glowColor = Vector4.Lerp(line.FlashColor, line.GlowColor,
                    MathHelper.Clamp((burstAge - expandTime) / (brightnessEnd - expandTime), 0f, 1f));
            }
            if (inFinale)
            {
                // 收尾阶段只重新放大光晕，暗底保持隐藏；每根线由 finaleDelay 错开。
                float finalePulse = localFinaleTime < finaleExpand
                    ? SakuraBurstCurve.Attack(localFinaleTime / finaleExpand, line.GlowAttackPower)
                    : 1f - Ease(MathHelper.Clamp((localFinaleTime - finaleExpand) / finaleShrink, 0f, 1f));
                glowWidth = MathHelper.Lerp(holdWidth, line.FinaleGlowPeakWidthScale, finalePulse);
                glowIntensity = MathHelper.Lerp(line.GlowHoldIntensity, line.FinaleGlowIntensity, finalePulse);
                coreIntensity = MathHelper.Lerp(.85f, 2.5f, finalePulse);
                darkWidth = darkOpacity = 0f;
            }
            if (!inFinale && darkWidth > 0f && darkOpacity > 0f)
            {
                DrawLayer(black, BlendState.NonPremultiplied, lengthProgress, darkWidth, black.Tint * darkOpacity);
                DrawLayer(red, BlendState.NonPremultiplied, lengthProgress, darkWidth, red.Tint * darkOpacity);
            }
            if (line == null || line.GlowHaloWidthScale > 0f)
            {
                float haloScale = line?.GlowHaloWidthScale ?? 1.4f;
                float haloIntensity = line?.GlowHaloIntensity ?? .32f;
                float haloPower = line?.GlowHaloPower ?? .85f;
                DrawLayer(white, BlendState.Additive, lengthProgress, glowWidth * haloScale,
                    glowColor * (glowIntensity * haloIntensity), 2f, haloPower);
            }
            DrawLayer(white, BlendState.Additive, lengthProgress, glowWidth,
                glowColor * glowIntensity, 1f, line?.GlowEdgePower ?? 1.7f);
            DrawLayer(white, BlendState.Additive, lengthProgress,
                glowWidth * MathHelper.Lerp(.45f, .78f, MathHelper.Clamp(coreIntensity / 2.5f, 0f, 1f)),
                new Vector4(1f, 1f, 1f, coreIntensity), 1f, line?.GlowCorePower ?? 2.4f);
        }
        finally
        {
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRasterizer;
            device.SamplerStates[0] = oldSampler;
            device.Textures[0] = oldTexture;
        }

        void DrawLayer(SakuraKiriLayer layer, BlendState blend, float lengthProgress, float extraWidth,
            Vector4? tintOverride = null, float glowMask = 0f, float glowPower = 1f)
        {
            Vector4 tint = tintOverride ?? layer.Tint;
            if (layer.Width <= 0f || layer.LengthScale <= 0f || tint.W <= 0f) return;

            float startX = -length * 0.5f;
            float visibleLength = length * layer.LengthScale * lengthProgress;
            float halfWidth = layer.Width * widthScale * extraWidth * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float x = startX + u * visibleLength;
                float y = MathF.Sin(MathHelper.Pi * u) * halfWidth;
                for (int side = 0; side < 2; side++)
                {
                    float localY = side == 0 ? -y : y;
                    Vector3 rotated = Vector3.Transform(new Vector3(x, localY, 0f), rotation);
                    float scale = focalLength / Math.Max(focalLength * .2f, focalLength + rotated.Z);
                    Vector2 position = center + new Vector2(rotated.X, rotated.Y) * scale;
                    vertices[i * 2 + side] = new VertexPositionTexture(new Vector3(position, 0f), new Vector2(u, side));
                }
            }

            effect.Parameters["TextureScale"].SetValue(layer.TextureScale);
            effect.Parameters["TextureOffset"].SetValue(layer.FlowSpeed * time);
            effect.Parameters["tint"].SetValue(tint);
            effect.Parameters["GlowMask"].SetValue(glowMask);
            effect.Parameters["GlowPower"].SetValue(Math.Max(.05f, glowPower));
            device.BlendState = blend;
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

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private void EnsureMeshBuffers(int segments)
    {
        if (vertices != null && vertices.Length == (segments + 1) * 2) return;
        vertices = new VertexPositionTexture[(segments + 1) * 2];
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
}
