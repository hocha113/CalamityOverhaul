using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>突进冲击和终点瞬斩共用帧龄，不依赖延长后的 projectile.timeLeft。</summary>
internal static class SakuraDashTiming
{
    public const float DashDistance = 800f;
    public static int DashTravelFrames => SakuraDashMotion.TravelFrames;
    public static int DashLineFrames => 30;
    // 冲击走过约 87% 路程时，在固定终点开始瞬斩。
    public static int SlashStartFrame => DashTravelFrames - 2;
    public static int SlashLifetimeFrames => 20;
    public static int ProjectileLifetime => SlashStartFrame + SlashLifetimeFrames + 1;

    public static float GetDashProgress(float age) => MathHelper.Clamp(age / DashTravelFrames, 0f, 1f);
    public static float GetLineTimeLeft(float age) => MathHelper.Clamp(DashLineFrames - age, 0f, DashLineFrames);
    public static float GetSlashAge(float age) => age - SlashStartFrame;
    public static bool IsSlashVisible(float age) => GetSlashAge(age) >= 0f && GetSlashAge(age) < SlashLifetimeFrames;
    public static Vector2 GetEndpoint(Vector2 origin, float rotation, float distance = DashDistance) =>
        origin + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * distance;
}

/// <summary>DashSlash 专用材质组合；仅使用本目录的资源与绘制实现。</summary>
public sealed class SakuraDashSlashRenderer : IDisposable
{
    public const string DefinitionPath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/DashSlash.json";
    public const string TexturePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/DashSlash";
    public const string EdgePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/DashSlash_Edge";
    public const string NoisePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/PerlinX";

    private readonly SakuraSlashRenderer renderer;
    private readonly SakuraDashCutLineRenderer cutLine;
    private readonly Vector2 pathStartToEnd;

    public SakuraDashSlashRenderer(GraphicsDevice device, RibbonDefinition definition,
        Texture2D texture, Texture2D edge, Texture2D noise, Effect effect,
        Texture2D lineTexture, Texture2D lineNoise, Effect fuzzyEdge, Texture2D lineDarkTexture)
    {
        // The JSON defines the ribbon geometry; the dash keeps its dedicated body and edge textures.
        renderer = new SakuraSlashRenderer(device, definition, texture, effect, noise, edge)
        {
            // 共享渲染器的此开关实际选择 BlendState.NonPremultiplied，覆盖 JSON 的 additive。
            ForceAlphaBlend = true,
            TintColor = new Vector3(1f, .22f, .25f),
            BladeColor = Vector3.One
        };
        cutLine = new SakuraDashCutLineRenderer(device, definition, lineTexture, lineNoise, fuzzyEdge, lineDarkTexture);
        var first = definition.Segments[0];
        var last = definition.Segments[^1];
        // 用路径中心线的端点判断 +U 的方向，不假设 JSON 一定竖直。
        Vector2 start = new((first.Outer[0].X + first.Inner[0].X) * .5f,
            (first.Outer[0].Y + first.Inner[0].Y) * .5f);
        Vector2 end = new((last.Outer[^1].X + last.Inner[^1].X) * .5f,
            (last.Outer[^1].Y + last.Inner[^1].Y) * .5f);
        pathStartToEnd = end - start;
    }

    private Vector2 GetDissolveDirection(Matrix localToScreen)
    {
        // 最终矩阵包含局部镜像和视图的重力翻转。方向指向屏幕上更低、
        // 应保留更久的端点；不能再额外乘 owner.gravDir，否则会重复翻转。
        Vector2 screenPath = Vector2.TransformNormal(pathStartToEnd, localToScreen);
        // 路径近乎水平时没有明确上下，稳定采用屏幕从左到右作为兜底。
        float down = MathF.Abs(screenPath.Y) > screenPath.Length() * .01f ? screenPath.Y : screenPath.X;
        return new Vector2(down >= 0f ? 1f : -1f, 0f);
    }

    /// <param name="age">整段突进的帧龄，与 DrawDashLine 使用相同时间轴。</param>
    public void Draw(Vector2 origin, float dashRotation, float age, Matrix worldToScreen,
        float scale = 1f, float angleOffset = 0f,
        bool drawCutLine = true, float cutLineLength = 380f, float cutLineWidth = 42f, float cutLineAngleOffset = 0f,
        float cutLineDarkWidthScale = 1f, float cutLineDarkOpacity = 1f,
        float dashDistance = SakuraDashTiming.DashDistance, bool gravityInverted = false)
    {
        if (!SakuraDashTiming.IsSlashVisible(age)) return;
        float slashAge = SakuraDashTiming.GetSlashAge(age);
        // 两帧闪出，前 3 帧保留完整刀刃，余下 17 帧沿路径从上端向下端消融。
        float opacity = MathHelper.Clamp((slashAge + 1f) / 2f, 0f, 1f);
        float dissolve = MathHelper.Clamp((slashAge - 3f) / (SakuraDashTiming.SlashLifetimeFrames - 3f), 0f, 1f);
        Vector2 endpoint = SakuraDashTiming.GetEndpoint(origin, dashRotation, dashDistance);
        bool mirrorX = MathF.Cos(dashRotation) < 0f;
        float shapeRotation = dashRotation - (mirrorX ? MathHelper.Pi : 0f) + angleOffset;
        // 左向仅镜像 X；反重力时局部镜像 Y，抵消游戏视图的 Y 翻转。
        Matrix transform = Matrix.CreateScale(mirrorX ? -scale : scale, gravityInverted ? -scale : scale, 1f)
            * Matrix.CreateRotationZ(shapeRotation)
            * Matrix.CreateTranslation(endpoint.X, endpoint.Y, 0f) * worldToScreen;
        renderer.Draw(transform, slashAge / 60f, opacity: opacity, dissolve: dissolve,
            attackUvOffset: 0f, dissolveDirection: GetDissolveDirection(transform),
            dissolveUvScale: new Vector2(1.5f, 1f), dissolveNoiseStrength: .35f,
            dissolveEdgeWidth: .08f, dissolveRimWidth: .015f,
            dissolveEdgeColor: new Vector3(1.6f, .3f, .55f), dissolveEdgeIntensity: .7f);
        if (drawCutLine)
            cutLine.Draw(endpoint, shapeRotation, slashAge, worldToScreen,
                scale, cutLineLength, cutLineWidth, cutLineAngleOffset, cutLineDarkWidthScale, cutLineDarkOpacity,
                mirrorX: mirrorX, mirrorY: gravityInverted);
    }

    public void Dispose()
    {
        renderer.Dispose();
        cutLine.Dispose();
    }
}
