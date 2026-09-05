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
        //分镜「溺月军阵」（2026-09 重做，同日按用户挑中的沙盒裁剪图整幅套用）：镜头推近约 2.2 倍，
        //仰视低地平线；巨大溺月坐在地平线上，两位大副影（约 0.8 与 1.0 屏高）夹在月盘两侧把月夹成一道门，
        //身后三排叠影铺满整条水线，全部是看不清面容的逆光剪影；原锚影退到画外，只剩伞沿压在右上角。
        //不给左列标题按钮留空白（用户裁定：文字压在剪影上不影响）
        /// <summary>水线高度（仰视机位：压到画面下四分之一）</summary>
        public const float HorizonY = 0.752f;
        /// <summary>溺月圆心：坐在地平线上、下沿沉入湖中，军阵立在它面前；两位副影夹在盘左右两缘</summary>
        public static readonly Vector2 MoonUv = new(0.56f, 0.53f);
        /// <summary>溺月盘半径（uv 纵向尺度，软边）；晕圈由着色器按倍数外扩</summary>
        public const float MoonRadius = 0.33f;
        /// <summary>近层满额视差（uv），远近元素按系数折减</summary>
        public static readonly Vector2 ParallaxMax = new(0.016f, 0.007f);

        //====== 立绘映射（Shenyo.png 258×544，沙盒校准）======
        public const int PortraitWidth = 258;
        public const int PortraitHeight = 544;
        public static readonly Vector2 PortraitTexel = new(1f / PortraitWidth, 1f / PortraitHeight);
        /// <summary>双目中心（立绘uv），瞳孔实测像素 (130,107)/(156,109) 的中点</summary>
        public static Vector2 EyeUv => new(0.5543f, 0.1985f);
        /// <summary>目距半宽（uv），瞳距 26px 之半</summary>
        public const float EyeSep = 0.0504f;
        /// <summary>
        /// 湖水线所在的立绘像素行：实测网袜 y≈452~463、靴口花边 y≈463~478、踝部 y≈505、鞋底 y=544。
        /// 取 464 即水淹到袜子中段：网袜露出上半截，花边袜口与整只靴子没入湖中（用户裁定 2026-09，
        /// 原 484 只没过脚踝、整段袜子外露，判为不够深）；此行以下没入湖中
        /// </summary>
        public const int WaterlineRow = 464;
        /// <summary>水线立绘 v（与 ShenyoMenuGhost.fx 的 uWaterV 共享）</summary>
        public const float WaterlineV = WaterlineRow / (float)PortraitHeight;

        /// <summary>湖上立影排布：X=uv横位 Depth=0远1近 Flip=翻面 Clarity=澄出本色量 Anchor=常驻锚影</summary>
        public readonly struct FigureDef(float x, float depth, bool flip, float clarity, bool anchor = false)
        {
            public readonly float X = x;
            public readonly float Depth = depth;
            public readonly bool Flip = flip;
            public readonly float Clarity = clarity;
            public readonly bool Anchor = anchor;
        }

        //分镜：远排四影混进军阵里生灭（其中一影正压在溺月盘心）；
        //左缘一影半身入画，两位大副影各压月盘一缘、把月夹成一道门（左 0.37 约 0.8 屏高，右 0.77 约满屏高）；
        //锚影中心在画外 x 1.10、水线在画外，只有伞沿压进右上角——"镜头边有个更大的人"的压迫读法。
        //澄色全部压到近零：面容藏在阴影里只剩瞳光（用户裁定 2026-09）
        //顺序即 uFeet 槽位：前四槽须是远影（着色器对这四槽只算涟漪波包、不算颤纹）
        public static readonly FigureDef[] Figures = [
            new(0.047f, 0.06f, true, 0.00f),
            new(0.297f, 0.10f, false, 0.00f),
            new(0.569f, 0.08f, true, 0.00f),
            new(0.930f, 0.12f, false, 0.00f),
            new(0.099f, 0.34f, true, 0.03f),
            new(0.370f, 0.48f, false, 0.04f),
            new(0.767f, 0.58f, true, 0.06f),
            new(1.100f, 0.93f, false, 0.08f, anchor: true),
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

        //「无数叠加的身影」军阵：三排剪影铺满整条水线（远排 12 / 中排 10 / 近排 7，两端出画），
        //远排约 0.15 屏高、近排约 0.30——伞与瞳光都读得出，彼此交叠、大半亮着暗淡瞳光，常驻不散；
        //月盘前的一段全部切成逆光剪影，月外的仍要比地平雾暗一档才读得出（雾色身贴雾色底=隐形）。
        //数据按深度升序绘制
        public static readonly CrowdDef[] Crowd = [
            //远排
            new(-0.006f, 0.024f, false, 0.80f, 0.45f),
            new(0.057f, 0.038f, true, 0.84f, 0.00f),
            new(0.183f, 0.024f, false, 0.74f, 0.36f),
            new(0.277f, 0.020f, true, 0.84f, 0.36f),
            new(0.366f, 0.017f, false, 0.78f, 0.51f),
            new(0.460f, 0.044f, true, 0.73f, 0.00f),
            new(0.549f, 0.044f, true, 0.83f, 0.43f),
            new(0.625f, 0.027f, true, 0.76f, 0.47f),
            new(0.749f, 0.041f, false, 0.80f, 0.45f),
            new(0.845f, 0.037f, true, 0.81f, 0.46f),
            new(0.933f, 0.030f, false, 0.73f, 0.32f),
            new(1.019f, 0.026f, true, 0.83f, 0.00f),
            //中排
            new(-0.011f, 0.077f, false, 0.89f, 0.44f),
            new(0.094f, 0.098f, true, 0.82f, 0.48f),
            new(0.210f, 0.085f, true, 0.86f, 0.52f),
            new(0.319f, 0.097f, true, 0.83f, 0.61f),
            new(0.459f, 0.062f, true, 0.91f, 0.50f),
            new(0.555f, 0.060f, false, 0.91f, 0.42f),
            new(0.668f, 0.093f, false, 0.89f, 0.41f),
            new(0.767f, 0.093f, true, 0.84f, 0.43f),
            new(0.885f, 0.092f, false, 0.88f, 0.36f),
            new(1.002f, 0.091f, false, 0.85f, 0.58f),
            //近排
            new(0.050f, 0.195f, false, 0.92f, 0.00f),
            new(0.197f, 0.154f, true, 0.93f, 0.00f),
            new(0.353f, 0.140f, false, 0.91f, 0.45f),
            new(0.528f, 0.184f, true, 0.95f, 0.00f),
            new(0.655f, 0.134f, false, 0.93f, 0.00f),
            new(0.826f, 0.148f, true, 0.96f, 0.48f),
            new(0.984f, 0.184f, false, 0.96f, 0.41f),
        ];

        /// <summary>
        /// 立影身高（屏高占比）：镜头推近 2.25 倍后的标尺——副影 depth 0.48 约 0.81、depth 0.58 约 1.04（顶到画外），
        /// 军阵远排约 0.15，锚影（0.93）约 1.8 只剩伞沿入画
        /// </summary>
        public static float FigureHeight(float depth) => 0.135f + 1.834f * MathF.Pow(depth, 1.32f);

        /// <summary>
        /// 立影水线接触点 y（uv）：身体在此没入湖面（立绘 <see cref="WaterlineV"/> 行对齐此处）。
        /// 副影 0.48/0.58 落在 0.924/0.977，两人脚下都留得住水面涟漪；锚影落在画外 1.2
        /// </summary>
        public static float FigureWaterlineY(float depth) => HorizonY + 0.0225f + 0.484f * MathF.Pow(depth, 1.6f);

        /// <summary>足下接触涟漪半径（透视空间尺度：横向半径≈此值×屏高像素，与湖面着色器约定一致）</summary>
        public static float FigureRingRadius(float depth) => 0.045f + FigureHeight(depth) * 0.16f;

        /// <summary>
        /// 逆光度：立影胸口落在溺月晕圈内的程度（0 月外 → 1 正压月盘）。
        /// 驱动着色器包圈缘光与剪影压黑，并折减大气透视——月前的身影是剪影不是雾影
        /// </summary>
        public static float FigureBacklit(float chestUvX, float chestUvY, float aspect) {
            Vector2 d = new((chestUvX - MoonUv.X) * aspect, chestUvY - MoonUv.Y);
            float r = d.Length() / (MoonRadius * 2.2f);
            return MathF.Exp(-r * r * 1.6f);
        }

        /// <summary>大气透视：越远越向潮雾靠拢</summary>
        public static float FigureHaze(float depth) => 0.55f * (1f - MathF.Pow(depth, 0.6f));

        /// <summary>蠕动幅度：远影更不安分</summary>
        public static float FigureWobble(float depth) => 1.15f - 0.30f * depth;

        /// <summary>距离模糊目标（屏幕像素）：远影糊成雾形，近影几乎为零；随镜头推近同步放大，军阵才保持雾影读法</summary>
        public static float FigureBlurPx(float depth) => 5.6f * MathF.Pow(1f - depth, 2.6f);

        /// <summary>屏幕像素模糊折算成立绘texel数（供 uBlur）</summary>
        public static float BlurTexels(float blurPx, float spriteScale)
            => MathHelper.Clamp(blurPx / MathF.Max(spriteScale, 0.001f), 0f, 60f);

        /// <summary>立影视差系数：与湖面月光路的透视视差同源（水线0.18→近岸0.85）</summary>
        public static float FigureParallax(float depth) {
            float dLake = MathHelper.Clamp((FigureWaterlineY(depth) - HorizonY) / (1f - HorizonY), 0f, 1f);
            return MathHelper.Lerp(0.18f, 0.85f, dLake);
        }
    }
}
