using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Ambience
{
    /// <summary>深度分级关键帧:某归一化深度处的光照/水色/氛围目标值</summary>
    internal readonly struct HadalGradeKey
    {
        /// <summary>归一化深度(HadalworldMetrics.DepthFraction 口径,海面=0)</summary>
        internal readonly float Frac;
        /// <summary>日光染色目标(物块光)与推力</summary>
        internal readonly Color SunTile;
        internal readonly float SunTileF;
        /// <summary>日光染色目标(背景)与推力,背景比物块暗得更快</summary>
        internal readonly Color SunBg;
        internal readonly float SunBgF;
        /// <summary>光照衰减率系数(1=原版;逐格复利,越低光源半径越小,VFX.md 裁定克制取值)</summary>
        internal readonly float Bright;
        /// <summary>滤镜浑浊纱色与浑浊度(屏幕向纱色收敛的力度)</summary>
        internal readonly Color Veil;
        internal readonly float Turbidity;
        /// <summary>丁达尔光束强度(日光带专属,暮光带上部残余)</summary>
        internal readonly float Rays;
        /// <summary>屏缘暗角强度(午夜带起,超深渊带最强)</summary>
        internal readonly float Vignette;
        /// <summary>海雪每探针命中概率</summary>
        internal readonly float Snow;

        internal HadalGradeKey(float frac, Color sunTile, float sunTileF, Color sunBg, float sunBgF,
            float bright, Color veil, float turbidity, float rays, float vignette, float snow) {
            Frac = frac;
            SunTile = sunTile;
            SunTileF = sunTileF;
            SunBg = sunBg;
            SunBgF = sunBgF;
            Bright = bright;
            Veil = veil;
            Turbidity = turbidity;
            Rays = rays;
            Vignette = vignette;
            Snow = snow;
        }
    }

    /// <summary>
    /// 深度光暗曲线配置表:调氛围只改此文件。关键帧位置全部由
    /// <see cref="HadalworldMetrics"/> 分带行界经 DepthFraction 推导,不写死行数(brief §2 协议)。
    /// 设计基调:日光带天光渐冷渐弱 → 暮光带残光 → 午夜带以下近全黑只剩光源半径,
    /// 浑浊度与暗角随深度单调上行,光色一路向蓝黑冷移
    /// </summary>
    internal static class HadalDepthProfile
    {
        internal static readonly HadalGradeKey[] Keys;

        //丁达尔光束的深度衰减跨度(px):海面到暮光带中部,光束在此跨度内衰减到零
        internal static readonly float RaySpanPx;

        static HadalDepthProfile() {
            //行→归一化深度(全表唯一的深度换算入口)
            static float F(int row) => HadalworldMetrics.DepthFraction(row * 16f);

            int sunlitMid = (HadalworldMetrics.SeaLevelRow + HadalworldMetrics.SunlitBottom) / 2;
            int twilightMid = (HadalworldMetrics.SunlitBottom + HadalworldMetrics.TwilightBottom) / 2;
            int midnightMid = (HadalworldMetrics.TwilightBottom + HadalworldMetrics.MidnightBottom) / 2;

            RaySpanPx = (twilightMid - HadalworldMetrics.SeaLevelRow) * 16f;

            //亮度系数换算参考:光圈半径比 ≈ ln(0.91)/ln(0.91*Bright)
            //0.97→79% 0.95→65% 0.93→56%;呼吸包络另在其上向下微压
            Keys = [
                //海面:无染色,轻纱,光束满强
                new(F(HadalworldMetrics.SeaLevelRow),
                    new Color(255, 255, 255), 0.00f, new Color(255, 255, 255), 0.00f, 1.000f,
                    new Color(86, 140, 150), 0.05f, 0.90f, 0.00f, 0.012f),
                //日光带中部:天光初染水色
                new(F(sunlitMid),
                    new Color(150, 205, 205), 0.25f, new Color(95, 160, 175), 0.35f, 1.000f,
                    new Color(62, 118, 132), 0.14f, 0.75f, 0.00f, 0.018f),
                //日光带底:光开始被水吃掉
                new(F(HadalworldMetrics.SunlitBottom),
                    new Color(105, 160, 180), 0.45f, new Color(60, 115, 140), 0.55f, 0.995f,
                    new Color(40, 88, 108), 0.24f, 0.35f, 0.00f, 0.024f),
                //暮光带中部:残光尽头的蓝
                new(F(twilightMid),
                    new Color(55, 95, 130), 0.62f, new Color(28, 60, 88), 0.72f, 0.985f,
                    new Color(20, 48, 68), 0.36f, 0.08f, 0.00f, 0.028f),
                //暮光带底:光束死绝
                new(F(HadalworldMetrics.TwilightBottom),
                    new Color(28, 52, 84), 0.78f, new Color(12, 28, 48), 0.85f, 0.975f,
                    new Color(10, 24, 40), 0.46f, 0.00f, 0.00f, 0.030f),
                //午夜带中部:近全黑,暗角起
                new(F(midnightMid),
                    new Color(12, 22, 44), 0.90f, new Color(5, 10, 22), 0.93f, 0.960f,
                    new Color(5, 11, 22), 0.55f, 0.00f, 0.05f, 0.030f),
                //午夜带底
                new(F(HadalworldMetrics.MidnightBottom),
                    new Color(6, 10, 26), 0.96f, new Color(2, 5, 12), 0.97f, 0.950f,
                    new Color(3, 6, 14), 0.60f, 0.00f, 0.09f, 0.028f),
                //深渊带底
                new(F(HadalworldMetrics.AbyssalBottom),
                    new Color(3, 5, 16), 0.985f, new Color(1, 2, 7), 0.99f, 0.940f,
                    new Color(2, 3, 9), 0.65f, 0.00f, 0.17f, 0.024f),
                //超深渊带最深可玩点:曲线尾端(以下封底基岩,平尾)
                new(F(HadalworldMetrics.DeepestPlayableRow),
                    new Color(2, 3, 12), 0.995f, new Color(1, 1, 5), 1.00f, 0.930f,
                    new Color(1, 2, 6), 0.68f, 0.00f, 0.25f, 0.020f),
            ];
        }

        /// <summary>归一化深度→分级插值采样(O(9) 线性走查,每 tick 少量调用)</summary>
        internal static HadalGradeKey Sample(float frac) {
            var keys = Keys;
            if (frac <= keys[0].Frac) {
                return keys[0];
            }
            for (int i = 0; i < keys.Length - 1; i++) {
                if (frac > keys[i + 1].Frac) {
                    continue;
                }
                float t = (frac - keys[i].Frac) / (keys[i + 1].Frac - keys[i].Frac);
                return new HadalGradeKey(frac,
                    Color.Lerp(keys[i].SunTile, keys[i + 1].SunTile, t),
                    MathHelper.Lerp(keys[i].SunTileF, keys[i + 1].SunTileF, t),
                    Color.Lerp(keys[i].SunBg, keys[i + 1].SunBg, t),
                    MathHelper.Lerp(keys[i].SunBgF, keys[i + 1].SunBgF, t),
                    MathHelper.Lerp(keys[i].Bright, keys[i + 1].Bright, t),
                    Color.Lerp(keys[i].Veil, keys[i + 1].Veil, t),
                    MathHelper.Lerp(keys[i].Turbidity, keys[i + 1].Turbidity, t),
                    MathHelper.Lerp(keys[i].Rays, keys[i + 1].Rays, t),
                    MathHelper.Lerp(keys[i].Vignette, keys[i + 1].Vignette, t),
                    MathHelper.Lerp(keys[i].Snow, keys[i + 1].Snow, t));
            }
            return keys[^1];
        }

        /// <summary>黑暗呼吸幅度:海面无呼吸,暮光带起渐入,深处 ≈0.03(乘在亮度系数上向下压)</summary>
        internal static float BreathAmp(float frac)
            => MathHelper.Lerp(0.006f, 0.030f, MathHelper.Clamp((frac - 0.06f) / 0.34f, 0f, 1f));

        /// <summary>
        /// 远景背景水色:浑浊纱色再压暗(背景比悬浮层暗)。
        /// 浅处水体透亮系数高,深处收敛到 0.45
        /// </summary>
        internal static Color SkyColor(float frac) {
            HadalGradeKey key = Sample(frac);
            float mul = MathHelper.Lerp(0.85f, 0.45f, MathHelper.Clamp(frac * 3f, 0f, 1f));
            return new Color(key.Veil.ToVector3() * mul);
        }

        /// <summary>生物微光资格权重:暮光带中部渐入,暮光带底起满额(深处才有"你看见了什么")</summary>
        internal static float GleamWeight(float frac) {
            float mid = HadalworldMetrics.DepthFraction(
                (HadalworldMetrics.SunlitBottom + HadalworldMetrics.TwilightBottom) / 2 * 16f);
            float full = HadalworldMetrics.DepthFraction(HadalworldMetrics.TwilightBottom * 16f);
            return MathHelper.Clamp((frac - mid) / MathHelper.Max(full - mid, 0.001f), 0f, 1f);
        }
    }
}
