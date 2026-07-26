using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 改铭台陈列刀身:鬼切本体贴图(<see cref="OnikiriItem"/>)以原生对角姿态、
    /// 整数倍缩放、零旋转陈列——像素网格不破;首帧对贴图作一次垂线扫描建立
    /// 刃缘/栋缘/中线轮廓表,铭位锚钉、引线搭点、刀鸣流光都贴真实剪影走。
    /// 绘制以"贴图内锚点(originPx)+锚点屏幕位+缩放"为变换,检分镜头缩放时锚点即不动焦点
    /// </summary>
    internal static class OniMeiBladeDraw
    {
        [VaultLoaden("CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriItem")]
        private static Asset<Texture2D> bladeTex = null;

        //====贴图轴锚(像素,量自 82x230 原图):锋尖(上右)→柄尾(下左)====
        private static readonly Vector2 SpriteTip = new(72.5f, 1f);
        private static readonly Vector2 SpritePommel = new(10f, 227f);
        /// <summary>贴图几何中心,陈列态的默认变换原点</summary>
        public static readonly Vector2 SpriteCenter = new(41f, 115f);
        /// <summary>贴图像素尺寸</summary>
        public static readonly Vector2 SpriteSize = new(82f, 230f);

        /// <summary>刀轴向(锋→柄尾)单位向量,贴图与屏幕同向(零旋转)</summary>
        public static Vector2 AxisDir => Vector2.Normalize(SpritePommel - SpriteTip);
        /// <summary>刀轴法向(+s 侧=栋/柄侧,屏幕左),引线自这一侧搭上来,刃侧留给流光</summary>
        public static Vector2 NormalDir { get { Vector2 d = AxisDir; return new Vector2(-d.Y, d.X); } }
        /// <summary>刀轴角(弧度)</summary>
        public static float AxisAngle => (SpritePommel - SpriteTip).ToRotation();
        /// <summary>刀上字形随刀轴立正的旋转(茎铭沿刀读)</summary>
        public static float GlyphRot => AxisAngle - MathHelper.PiOver2;

        public static bool Ready => bladeTex?.Value != null;

        //====轮廓表:沿轴 u∈[0,1] 采样,+s = 栋/柄侧====
        private const int Samples = 48;
        private static bool profileBuilt;
        private static readonly float[] edgeOff = new float[Samples];   //刃缘(负侧)
        private static readonly float[] backOff = new float[Samples];   //栋/柄缘(正侧)
        private static readonly float[] centerOff = new float[Samples]; //剪影中线
        private static readonly float[] redGlow = new float[Samples];   //绯纹权重

        private static void EnsureProfile() {
            if (profileBuilt || !Ready) {
                return;
            }
            profileBuilt = true;
            //兜底剖面:采样失败时给一根 ±7px 的直刃
            for (int i = 0; i < Samples; i++) {
                edgeOff[i] = -7f;
                backOff[i] = 7f;
                centerOff[i] = 0f;
                redGlow[i] = 0f;
            }
            try {
                Texture2D tex = bladeTex.Value;
                Color[] data = new Color[tex.Width * tex.Height];
                tex.GetData(data);
                Vector2 axis = SpritePommel - SpriteTip;
                Vector2 normal = NormalDir;
                for (int i = 0; i < Samples; i++) {
                    float u = i / (Samples - 1f);
                    Vector2 p = SpriteTip + axis * u;
                    float sMin = float.MaxValue, sMax = float.MinValue, red = 0f;
                    for (float s = -70f; s <= 70f; s += 1f) {
                        Vector2 q = p + normal * s;
                        int x = (int)MathF.Round(q.X);
                        int y = (int)MathF.Round(q.Y);
                        if (x < 0 || y < 0 || x >= tex.Width || y >= tex.Height) {
                            continue;
                        }
                        Color c = data[y * tex.Width + x];
                        if (c.A <= 24) {
                            continue;
                        }
                        sMin = MathF.Min(sMin, s);
                        sMax = MathF.Max(sMax, s);
                        red = MathF.Max(red, (c.R - MathF.Max(c.G, c.B)) / 255f);
                    }
                    if (sMax >= sMin) {
                        edgeOff[i] = sMin;
                        backOff[i] = sMax;
                        centerOff[i] = (sMin + sMax) * 0.5f;
                        redGlow[i] = MathHelper.Clamp(red * 1.6f, 0f, 1f);
                    }
                }
            }
            catch {
                //GetData 偶发失败(设备丢失等)走兜底直刃,下帧不重试
            }
        }

        private static float Sample(float[] arr, float u) {
            EnsureProfile();
            float f = MathHelper.Clamp(u, 0f, 1f) * (Samples - 1);
            int i = Math.Min((int)f, Samples - 2);
            return MathHelper.Lerp(arr[i], arr[i + 1], f - i);
        }

        /// <summary>u 处绯纹权重 0~1,呼吸妖光的分布</summary>
        public static float RedGlow(float u) => Sample(redGlow, u);
        /// <summary>u 处剪影厚度(贴图 px)</summary>
        public static float Thickness(float u) => Sample(backOff, u) - Sample(edgeOff, u);

        /// <summary>u 处剪影中线点(贴图 px);铭位锚/刻痕落点</summary>
        public static Vector2 SpinePx(float u)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * Sample(centerOff, u);

        /// <summary>u 处刃缘点(贴图 px),standoff 向刃外让出;流光走这侧</summary>
        public static Vector2 EdgePx(float u, float standoff = 0f)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * (Sample(edgeOff, u) - standoff);

        /// <summary>u 处栋/柄缘点(贴图 px),standoff 向外让出;锚钉/引线搭点走这侧</summary>
        public static Vector2 BackPx(float u, float standoff = 0f)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * (Sample(backOff, u) + standoff);

        /// <summary>u 处刃缘切向角(弧度,贴图与屏幕同向)</summary>
        public static float EdgeTangent(float u) {
            const float D = 0.03f;
            return (EdgePx(u + D) - EdgePx(u - D)).ToRotation();
        }

        /// <summary>
        /// 原生姿态绘制:originPx=贴图内锚(缩放的不动点),screenPos=锚的屏幕位;
        /// 剪影落影垫底,绯纹段透一层呼吸妖光;批内 Deferred+PointClamp 保像素锐利
        /// </summary>
        public static void Draw(SpriteBatch sb, Vector2 originPx, Vector2 screenPos, float scale,
            float alpha, float time) {
            if (!Ready) {
                return;
            }
            EnsureProfile();
            Texture2D tex = bladeTex.Value;

            //剪影落影:本体黑染错位一截,深度暗示(非同心扩层)
            sb.Draw(tex, screenPos + new Vector2(2.5f, 6f) * MathF.Max(scale * 0.5f, 1f), null,
                new Color(8, 2, 5) * (alpha * 0.55f), 0f, originPx, scale, SpriteEffects.None, 0f);
            //本体(零旋转,整数倍时像素完全对齐)
            sb.Draw(tex, screenPos, null, Color.White * alpha, 0f, originPx, scale, SpriteEffects.None, 0f);

            //绯纹妖光:红纹段上一层呼吸软辉,黑刃在烛下低低透红
            float breath = 0.72f + 0.28f * (float)Math.Sin(time * 1.35f + 0.8f);
            for (int i = 0; i < 7; i++) {
                float u = 0.42f + i * 0.06f;
                float w = RedGlow(u);
                if (w <= 0.05f) {
                    continue;
                }
                Vector2 pos = screenPos + (SpinePx(u) - originPx) * scale;
                float r = Thickness(u) * scale * (0.7f + w * 0.5f);
                OniBrush.DrawSoftDot(sb, pos, r, OnikiriUITheme.Bright, alpha * 0.085f * w * breath);
            }
        }
    }
}
