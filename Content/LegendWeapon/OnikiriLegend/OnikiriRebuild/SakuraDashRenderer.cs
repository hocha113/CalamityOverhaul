using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>集中绘制整段冲刺特效；资源借用，图形对象由本类释放。</summary>
public sealed class SakuraDashRenderer : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly Texture2D ringTexture;
    private readonly SakuraDashRingRenderer ring;
    private readonly SakuraDashWindRenderer wind;
    private readonly SakuraDashSlashRenderer slash;

    public SakuraDashRenderer(GraphicsDevice device, RibbonDefinition definition,
        Texture2D ringTexture, Texture2D windTexture, Texture2D body, Texture2D edge, Texture2D noise,
        Effect slashEffect, Texture2D line, Texture2D lineNoise, Effect lineEffect, Texture2D lineDark)
    {
        this.device = device;
        this.ringTexture = ringTexture;
        ring = new SakuraDashRingRenderer(device);
        wind = new SakuraDashWindRenderer(device, windTexture, ringTexture);
        slash = new SakuraDashSlashRenderer(device, definition, body, edge, noise, slashEffect,
            line, lineNoise, lineEffect, lineDark);
    }

    public void Draw(Vector2 origin, float rotation, float age, float distance, Matrix worldToScreen, bool gravityInverted = false)
    {
        Vector2 direction = new(MathF.Cos(rotation), MathF.Sin(rotation));
        ring.Draw(device, ringTexture, origin - direction * 100f, direction, age, 20f, worldToScreen,
            startAngle: 0f, rotationSpeed: -.135f, radiusStart: 186f, radiusEnd: 252f,
            depthStart: 16f, depthEnd: 28f, thicknessStart: 62f, thicknessEnd: 45f,
            openingAngle: -.7f, tipFraction: .16f);
        ring.Draw(device, ringTexture, origin, direction, age, 20f, worldToScreen,
            startAngle: 0f, rotationSpeed: -.135f, radiusStart: 93f, radiusEnd: 126f,
            depthStart: 16f, depthEnd: 28f, thicknessStart: 22f, thicknessEnd: 35f,
            openingAngle: 1.7f, tipFraction: .16f);
        wind.Draw(origin, direction, age, distance, worldToScreen);
        slash.Draw(origin, rotation, age, worldToScreen,
            cutLineLength: 580f, cutLineWidth: 42f, dashDistance: distance, gravityInverted: gravityInverted);
    }

    public void Dispose()
    {
        ring.Dispose();
        wind.Dispose();
        slash.Dispose();
    }
}
