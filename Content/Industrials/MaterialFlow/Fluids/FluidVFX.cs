using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids
{
    /// <summary>
    /// 单液型的视觉身份参数。材质命名:水=冷淡水(快泡碎光),岩浆=熔浆(黑壳骑亮体慢涌),
    /// 蜜=稠蜜(懒泡拉丝),微光=星尘悬液(星屑悬浮虹彩)
    /// </summary>
    internal readonly struct FluidStyle(Color bright, Color main, Color deep,
        float bubbleRate, float bubbleSpeed, float bubbleScale,
        float waveAmp, float waveSpeed, float glassy, float crust, float sparkle, float glow)
    {
        /// <summary>高光/翻沫色</summary>
        internal readonly Color Bright = bright;
        /// <summary>主体色</summary>
        internal readonly Color Main = main;
        /// <summary>深部/暗缘色</summary>
        internal readonly Color Deep = deep;
        /// <summary>气泡密度 0..1</summary>
        internal readonly float BubbleRate = bubbleRate;
        /// <summary>气泡上升速度倍率</summary>
        internal readonly float BubbleSpeed = bubbleSpeed;
        /// <summary>气泡尺寸倍率</summary>
        internal readonly float BubbleScale = bubbleScale;
        /// <summary>液面波幅(px)</summary>
        internal readonly float WaveAmp = waveAmp;
        /// <summary>液面波速倍率</summary>
        internal readonly float WaveSpeed = waveSpeed;
        /// <summary>粘稠度 0..1(喂 FluidPour uGlassy 与管道慢液分桶)</summary>
        internal readonly float Glassy = glassy;
        /// <summary>黑壳浮斑 0..1(岩浆专属)</summary>
        internal readonly float Crust = crust;
        /// <summary>星屑 0..1(微光专属)</summary>
        internal readonly float Sparkle = sparkle;
        /// <summary>自发光强度 0..1(岩浆/微光的暗处可读性)</summary>
        internal readonly float Glow = glow;
    }

    /// <summary>液体系视觉共用:液型风格表 / 哈希 / 加色辅助 / 共享液窗绘制</summary>
    internal static class FluidVFX
    {
        private static readonly FluidStyle Water = new(
            new Color(200, 235, 255), new Color(62, 138, 235), new Color(14, 44, 108),
            bubbleRate: 0.9f, bubbleSpeed: 1.0f, bubbleScale: 0.8f,
            waveAmp: 1.7f, waveSpeed: 1.0f, glassy: 0f, crust: 0f, sparkle: 0f, glow: 0.12f);

        private static readonly FluidStyle Lava = new(
            new Color(255, 226, 120), new Color(255, 108, 22), new Color(84, 18, 6),
            bubbleRate: 0.35f, bubbleSpeed: 0.28f, bubbleScale: 1.7f,
            waveAmp: 1.0f, waveSpeed: 0.26f, glassy: 1f, crust: 1f, sparkle: 0f, glow: 1f);

        private static readonly FluidStyle Honey = new(
            new Color(255, 228, 142), new Color(230, 162, 40), new Color(118, 68, 10),
            bubbleRate: 0.25f, bubbleSpeed: 0.18f, bubbleScale: 1.5f,
            waveAmp: 0.8f, waveSpeed: 0.34f, glassy: 1f, crust: 0f, sparkle: 0f, glow: 0.2f);

        private static readonly FluidStyle Shimmer = new(
            new Color(244, 206, 255), new Color(198, 122, 252), new Color(82, 40, 142),
            bubbleRate: 0.6f, bubbleSpeed: 0.5f, bubbleScale: 0.7f,
            waveAmp: 1.3f, waveSpeed: 0.62f, glassy: 0.2f, crust: 0f, sparkle: 1f, glow: 0.7f);

        internal static FluidStyle GetStyle(int liquidId) => liquidId switch {
            LiquidID.Lava => Lava,
            LiquidID.Honey => Honey,
            LiquidID.Shimmer => Shimmer,
            _ => Water,
        };

        /// <summary>慢液(浆/蜜),管道流团走慢批</summary>
        internal static bool IsSlowFluid(int liquidId) => liquidId is LiquidID.Lava or LiquidID.Honey;

        /// <summary>确定性哈希 0..1</summary>
        internal static float Hash01(int n) {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)int.MaxValue;
        }

        /// <summary>A=0 加色染色(AlphaBlend 预乘批里的提亮层)</summary>
        internal static Color Glow(Color c, float k) {
            k = MathHelper.Clamp(k, 0f, 1f);
            return new Color((int)(c.R * k), (int)(c.G * k), (int)(c.B * k), 0);
        }

        /// <summary>粒子/表现的玩家距离门(平方像素),屏外机器不发粒子</summary>
        internal const float ParticleRangeSQ = 1200f * 1200f;
        internal static bool NearLocalPlayer(Vector2 worldPos)
            => !Main.dedServ && Main.LocalPlayer.DistanceSQ(worldPos) < ParticleRangeSQ;

        /// <summary>
        /// 共享液窗:观察窗内的液体本体+液面波列+高光线+按液型的气泡/黑壳/星屑。
        /// 储罐/岩浆发电机/微光转化槽共用一支笔。
        /// rect=窗内区域(屏幕坐标),ratio=充盈度,animTime=秒(待机冻结由调用方停表),
        /// activity=充放活跃度 0..1(抬波幅提亮液面)
        /// </summary>
        internal static void DrawLiquidWindow(SpriteBatch sb, Rectangle rect, int liquidType,
            float ratio, float animTime, float activity, int seed) {
            if (ratio <= 0.004f || rect.Width < 4 || rect.Height < 4) {
                return;
            }
            FluidStyle style = GetStyle(liquidType);
            Texture2D px = VaultAsset.placeholder2.Value;

            float fluidH = rect.Height * MathHelper.Clamp(ratio, 0f, 1f);
            float surfaceY = rect.Bottom - fluidH;

            //本体两段:上段主色,下段沉向深色(圆柱纵深感)
            int upperH = (int)(fluidH * 0.45f);
            int lowerH = (int)fluidH - upperH;
            Color upper = Color.Lerp(style.Main, style.Deep, 0.18f) * 0.92f;
            Color lower = Color.Lerp(style.Main, style.Deep, 0.62f) * 0.95f;
            sb.Draw(px, new Rectangle(rect.X, (int)surfaceY, rect.Width, upperH), upper);
            sb.Draw(px, new Rectangle(rect.X, (int)surfaceY + upperH, rect.Width, lowerH), lower);

            //液面波列:逐列相位错开,活跃时波幅抬升
            float amp = style.WaveAmp * (1f + activity * 1.6f);
            const int cols = 12;
            float colW = rect.Width / (float)cols;
            for (int i = 0; i < cols; i++) {
                float phase = Hash01(seed * 31 + i) * MathHelper.TwoPi;
                float w = MathF.Sin(animTime * 2.4f * style.WaveSpeed + i * 1.55f + phase) * amp;
                float top = surfaceY + w;
                //波峰补块(把波形补进本体)
                float h = surfaceY + 3f - top;
                if (h > 0.5f) {
                    sb.Draw(px, new Rectangle(rect.X + (int)(i * colW), (int)top, (int)MathF.Ceiling(colW), (int)h), upper);
                }
                //波峰高光点
                sb.Draw(px, new Rectangle(rect.X + (int)(i * colW), (int)top, (int)MathF.Ceiling(colW), 1),
                    Glow(style.Bright, 0.30f + 0.25f * activity + 0.12f * MathF.Sin(animTime * 3.1f + phase)));
            }

            //气泡/星屑:确定性相位循环,零分配
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            int count = (int)(style.BubbleRate * 7f) + 2;
            for (int i = 0; i < count; i++) {
                float h1 = Hash01(seed * 131 + i * 17);
                float h2 = Hash01(seed * 197 + i * 29);
                float cycle = 3.2f / MathHelper.Max(style.BubbleSpeed, 0.05f) * (0.7f + h1 * 0.6f);
                float t = (animTime / cycle + h2) % 1f;
                float bx = rect.X + (0.08f + 0.84f * Hash01(seed * 61 + i * 7)) * rect.Width
                    + MathF.Sin(animTime * 1.7f + i) * 1.5f;
                float by;
                float alpha;
                if (style.Sparkle > 0.5f) {
                    //星尘:全域悬浮明灭,缓慢上飘
                    by = rect.Bottom - fluidH * (0.08f + 0.86f * ((h1 + t * 0.35f) % 1f));
                    alpha = 0.25f + 0.75f * MathF.Pow(MathF.Abs(MathF.Sin((t * 3f + h2) * MathHelper.Pi)), 3f);
                }
                else {
                    //气泡:自底升至液面,近液面时消散
                    by = rect.Bottom - 2f - (fluidH - 4f) * t;
                    alpha = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
                }
                if (by < surfaceY + 1f) {
                    continue;
                }
                float size = (3.2f + h2 * 2.6f) * style.BubbleScale;
                float scale = size / glowTex.Width * 2f;
                sb.Draw(glowTex, new Vector2(bx, by), null, Glow(style.Bright, 0.5f * alpha),
                    0f, glowTex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }

            //岩浆黑壳浮斑:真 alpha 暗层贴着液面漂(Extra_98,黑底贴图画不出暗形)
            if (style.Crust > 0.5f) {
                Texture2D crustTex = CWRAsset.Extra_98.Value;
                for (int i = 0; i < 3; i++) {
                    float drift = ((animTime * 0.02f * (0.5f + Hash01(seed + i) * 0.8f) + Hash01(seed * 7 + i)) % 1f);
                    float cxp = rect.X + drift * rect.Width;
                    float cyp = surfaceY + 2.5f + Hash01(seed * 13 + i) * MathF.Min(6f, fluidH * 0.3f);
                    sb.Draw(crustTex, new Vector2(cxp, cyp), null, new Color(20, 8, 4, 200), MathHelper.PiOver2,
                        crustTex.Size() * 0.5f, new Vector2(0.05f, 0.16f + Hash01(seed * 3 + i) * 0.1f), SpriteEffects.None, 0f);
                }
            }

            //液面高光线:主线+错位次级线
            sb.Draw(px, new Rectangle(rect.X, (int)surfaceY, rect.Width, 1),
                Glow(style.Bright, 0.5f + 0.3f * activity));
            int subW = (int)(rect.Width * 0.4f);
            int subX = rect.X + (int)((rect.Width - subW) * (0.5f + 0.4f * MathF.Sin(animTime * 0.8f)));
            sb.Draw(px, new Rectangle(subX, (int)surfaceY + 1, subW, 1), Glow(style.Bright, 0.22f));
        }
    }
}
