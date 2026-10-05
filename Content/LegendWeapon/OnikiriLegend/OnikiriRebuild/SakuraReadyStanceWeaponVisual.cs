using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal static class SakuraReadyStanceWeaponVisual
{
    internal static Vector2 WeaponOffset => new(-65,70);//-60,-70
    internal static float WeaponRotation => 3.7f;
    internal static float KiriGlowStartProgress => .08f;
    internal static float KiriGlowRisePower => 1.35f;
    public static int BurstFrame => 30;
    private static int LungeFrame => BurstFrame - 1;
    private static int RecoveryFrames => 16;
    private static int GlowStartFrame => 5;
    private static int GlowFadeFrames => 19;
    internal const int GlowPadding = 12;
    private const int BlurRadius = 8;
    private static float LungeDistance => 35f;
    private static Vector2 LungeDirection => Vector2.Normalize(new Vector2(1f, -1f));

    public static Vector2 GetLungeOffset(int frame)
    {
        if (frame < LungeFrame || frame >= LungeFrame + RecoveryFrames)
            return Vector2.Zero;

        float recovery = (frame - LungeFrame) / (float)RecoveryFrames;
        float remaining = 1f - recovery;
        return LungeDirection * (LungeDistance * remaining * remaining * remaining);
    }

    public static float GetGlowStrength(int frame)
    {
        if (frame <= GlowStartFrame || frame >= LungeFrame + GlowFadeFrames)
            return 0f;

        if (frame <= LungeFrame)
        {
            float charge = (frame - GlowStartFrame) / (float)(LungeFrame - GlowStartFrame);
            return MathF.Pow(charge, 1.5f);
        }

        float fade = (frame - LungeFrame) / (float)GlowFadeFrames;
        return 1f - fade * fade * (3f - 2f * fade);
    }

    public static float GetGrowingGlowStrength(float progress, float startProgress, float risePower)
    {
        float start = MathHelper.Clamp(startProgress, 0f, .99f);
        float charge = MathHelper.Clamp((progress - start) / (1f - start), 0f, 1f);
        return MathF.Pow(charge, Math.Max(.01f, risePower));
    }

    public static Texture2D CreateGlowMask(GraphicsDevice device, Texture2D weapon)
    {
        Color[] source = new Color[weapon.Width * weapon.Height];
        weapon.GetData(source);
        int width = weapon.Width + GlowPadding * 2;
        int height = weapon.Height + GlowPadding * 2;
        float[] horizontal = new float[width * height];
        float[] weights = new float[BlurRadius * 2 + 1];
        float totalWeight = 0f;
        for (int i = -BlurRadius; i <= BlurRadius; i++)
        {
            float weight = MathF.Exp(-i * i / (2f * 3f * 3f));
            weights[i + BlurRadius] = weight;
            totalWeight += weight;
        }
        for (int i = 0; i < weights.Length; i++) weights[i] /= totalWeight;

        for (int y = GlowPadding; y < height - GlowPadding; y++)
        for (int x = 0; x < width; x++)
        {
            float value = 0f;
            for (int k = -BlurRadius; k <= BlurRadius; k++)
            {
                int sourceX = x - GlowPadding + k;
                if ((uint)sourceX < (uint)weapon.Width)
                    value += source[(y - GlowPadding) * weapon.Width + sourceX].A * weights[k + BlurRadius];
            }
            horizontal[y * width + x] = value;
        }

        Color[] blurred = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float value = 0f;
            for (int k = -BlurRadius; k <= BlurRadius; k++)
            {
                int sourceY = y + k;
                if ((uint)sourceY < (uint)height)
                    value += horizontal[sourceY * width + x] * weights[k + BlurRadius];
            }
            byte alpha = (byte)MathF.Min(255f, value * 2.4f);
            blurred[y * width + x] = new Color(alpha, (byte)(alpha * .07f), (byte)(alpha * .1f), alpha);
        }

        Texture2D mask = new(device, width, height);
        mask.SetData(blurred);
        return mask;
    }

    public static Color GlowColor(float strength) => Color.White * (strength * .95f);

    // The same alpha-blended glow tint is used by the player layer and the FNA preview.
    public static void DrawGlow(SpriteBatch batch, Texture2D mask, Vector2 center,
        float rotation, SpriteEffects effects, float strength)
    {
        if (strength <= 0f) return;

        Vector2 origin = new(mask.Width * .5f, mask.Height * .5f);
        batch.Draw(mask, center, null, GlowColor(strength),
            rotation, origin, 1f, effects, 0f);
    }
}
