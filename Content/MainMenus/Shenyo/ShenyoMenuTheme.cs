using System;

namespace CalamityOverhaul.Content.MainMenus.Shenyo
{
    /// <summary>鬼湖夜雨主菜单调色板、布局与场景常量；菜单绘制路径内 screenWidth/mouse 已是 UI 空间，直接取用</summary>
    internal static class ShenyoMenuTheme
    {
        //====== 湿墨色板（承 OniRainSky/ShenyoRainForm 系）======
        //正文惨白与暗态
        public static readonly Color TextPale = new(222, 232, 236);
        public static readonly Color TextDim = new(140, 158, 166);
        //悬停高亮：湿墨冷青（径流水光同源）
        public static readonly Color AccentWater = new(136, 202, 216);
        //次级细线：溺月惨白
        public static readonly Color AccentMoon = new(196, 214, 218);
        //潮雾贴片染色（Fog 真alpha图直接乘色）
        public static readonly Color MistTint = new(118, 144, 152);
        //CPU回退用的天穹上下底色
        public static readonly Color FallbackSkyTop = new(5, 7, 9);
        public static readonly Color FallbackSkyHorizon = new(47, 57, 59);
        public static readonly Color FallbackWaterDeep = new(4, 5, 7);
        //CPU回退立影压色
        public static readonly Color FallbackMurk = new(20, 26, 30);

        //====== 标题与按钮布局（与夜樱主题同锚，保持主题间肌肉记忆）======
        public const float ButtonAnchorX = 92f;
        public const float ButtonSpacing = 54f;
        public const float ButtonTextScale = 0.62f;
        public const float ButtonHoverScaleBonus = 0.07f;
        public const float ButtonHoverSlide = 14f;
        public const float TitleX = 92f;
        public const float TitleY = 84f;

        //====== 场景几何（uv 空间，与 ShenyoMenuLake.fx 共享）======
        /// <summary>水线高度</summary>
        public const float HorizonY = 0.62f;
        /// <summary>溺月圆心</summary>
        public static readonly Vector2 MoonUv = new(0.66f, 0.24f);
        /// <summary>近层满额视差（uv），远近元素按系数折减</summary>
        public static readonly Vector2 ParallaxMax = new(0.016f, 0.007f);

        //====== 立绘映射（Shenyo.png 258×544，沙盒校准）======
        public static readonly Vector2 PortraitTexel = new(1f / 258f, 1f / 544f);
        /// <summary>双目中心（立绘uv）</summary>
        public static readonly Vector2 EyeUv = new(0.541f, 0.204f);
        /// <summary>目距半宽（uv）</summary>
        public const float EyeSep = 0.0485f;

        /// <summary>湖上立影排布：X=uv横位 Depth=0远1近 Flip=翻面 Clarity=澄出本色量 Anchor=常驻锚影</summary>
        public readonly struct FigureDef(float x, float depth, bool flip, float clarity, bool anchor = false)
        {
            public readonly float X = x;
            public readonly float Depth = depth;
            public readonly bool Flip = flip;
            public readonly float Clarity = clarity;
            public readonly bool Anchor = anchor;
        }

        //分镜：远排四影散在水线月光路两侧，中排两影拉开纵深，
        //近中一影压在光路旁，右侧大近影为常驻锚——左列留给标题与按钮
        public static readonly FigureDef[] Figures = [
            new(0.545f, 0.05f, false, 0.00f),
            new(0.615f, 0.09f, true, 0.00f),
            new(0.700f, 0.13f, false, 0.02f),
            new(0.455f, 0.08f, true, 0.00f),
            new(0.385f, 0.36f, true, 0.06f),
            new(0.795f, 0.42f, false, 0.08f),
            new(0.575f, 0.62f, false, 0.12f),
            new(0.875f, 0.93f, false, 0.25f, anchor: true),
        ];

        /// <summary>水线叠影群：X=uv横位 Depth=0远1近 Flip=翻面 Alpha=体透明度 EyeMul=目芒倍率（0=无目）</summary>
        public readonly struct CrowdDef(float x, float depth, bool flip, float alpha, float eyeMul)
        {
            public readonly float X = x;
            public readonly float Depth = depth;
            public readonly bool Flip = flip;
            public readonly float Alpha = alpha;
            public readonly float EyeMul = eyeMul;
        }

        //「无数叠加的身影」：贴着水线的一排微缩剪影，彼此交叠沉在雾里，
        //只有零星几双淡眼——常驻不散，柔糊但要比地平雾更暗一档才读得出（雾色身贴雾色底=隐形）
        public static readonly CrowdDef[] Crowd = [
            new(0.365f, 0.020f, false, 0.78f, 0.00f),
            new(0.412f, 0.055f, true, 0.87f, 0.60f),
            new(0.438f, 0.028f, false, 0.80f, 0.00f),
            new(0.492f, 0.070f, true, 0.90f, 0.00f),
            new(0.522f, 0.018f, false, 0.74f, 0.50f),
            new(0.578f, 0.040f, true, 0.84f, 0.00f),
            new(0.596f, 0.075f, false, 0.92f, 0.65f),
            new(0.648f, 0.024f, true, 0.76f, 0.00f),
            new(0.672f, 0.060f, false, 0.87f, 0.00f),
            new(0.735f, 0.034f, true, 0.80f, 0.55f),
            new(0.762f, 0.080f, false, 0.92f, 0.00f),
            new(0.828f, 0.046f, true, 0.84f, 0.00f),
            new(0.858f, 0.022f, false, 0.74f, 0.45f),
            new(0.910f, 0.058f, true, 0.85f, 0.00f),
        ];

        /// <summary>立影身高（屏高占比）</summary>
        public static float FigureHeight(float depth) => 0.055f + 0.545f * MathF.Pow(depth, 1.32f);

        /// <summary>立影足点 y（uv）</summary>
        public static float FigureFeetY(float depth) => HorizonY + 0.012f + 0.42f * MathF.Pow(depth, 1.6f);

        /// <summary>足下接触涟漪半径（uv 纵向尺度，与湖面着色器约定一致）</summary>
        public static float FigureRingRadius(float depth) => 0.02f + FigureHeight(depth) * 0.22f;

        /// <summary>大气透视：越远越向潮雾靠拢</summary>
        public static float FigureHaze(float depth) => 0.55f * (1f - MathF.Pow(depth, 0.6f));

        /// <summary>蠕动幅度：远影更不安分</summary>
        public static float FigureWobble(float depth) => 1.15f - 0.30f * depth;

        /// <summary>距离模糊目标（屏幕像素）：远影糊成雾形，锚影几乎为零</summary>
        public static float FigureBlurPx(float depth) => 2.8f * MathF.Pow(1f - depth, 2.6f);

        /// <summary>屏幕像素模糊折算成立绘texel数（供 uBlur）</summary>
        public static float BlurTexels(float blurPx, float spriteScale)
            => MathHelper.Clamp(blurPx / MathF.Max(spriteScale, 0.001f), 0f, 60f);

        /// <summary>立影视差系数：与湖面月光路的透视视差同源（水线0.18→近岸0.85）</summary>
        public static float FigureParallax(float depth) {
            float dLake = MathHelper.Clamp((FigureFeetY(depth) - HorizonY) / (1f - HorizonY), 0f, 1f);
            return MathHelper.Lerp(0.18f, 0.85f, dLake);
        }
    }
}
