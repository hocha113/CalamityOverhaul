using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 改铭台陈列刀身:鬼切本体贴图(<see cref="OnikiriItem"/>)以原生对角姿态、
    /// 整数倍缩放、零旋转陈列——像素网格不破;剪影轮廓表由
    /// <see cref="OniBladeProfile"/> 共用(与在世刀身铭刻层同一份扫描)。
    /// 绘制以"贴图内锚点(originPx)+锚点屏幕位+缩放"为变换,检分镜头缩放时锚点即不动焦点
    /// </summary>
    internal static class OniMeiBladeDraw
    {
        /// <summary>贴图几何中心,陈列态的默认变换原点</summary>
        public static Vector2 SpriteCenter => OniBladeProfile.SpriteCenter;
        /// <summary>贴图像素尺寸</summary>
        public static Vector2 SpriteSize => OniBladeProfile.SpriteSize;

        /// <summary>刀轴向(锋→柄尾)单位向量,贴图与屏幕同向(零旋转)</summary>
        public static Vector2 AxisDir => OniBladeProfile.AxisDir;
        /// <summary>刀轴法向(+s 侧=栋/柄侧,屏幕左),引线自这一侧搭上来,刃侧留给流光</summary>
        public static Vector2 NormalDir => OniBladeProfile.NormalDir;
        /// <summary>刀轴角(弧度)</summary>
        public static float AxisAngle => OniBladeProfile.AxisAngle;
        /// <summary>刀上字形随刀轴立正的旋转(茎铭沿刀读)</summary>
        public static float GlyphRot => OniBladeProfile.GlyphRot;

        public static bool Ready => OniBladeProfile.Ready;

        /// <summary>u 处绯纹权重 0~1,呼吸妖光的分布</summary>
        public static float RedGlow(float u) => OniBladeProfile.RedGlow(u);
        /// <summary>u 处剪影厚度(贴图 px)</summary>
        public static float Thickness(float u) => OniBladeProfile.Thickness(u);
        /// <summary>u 处剪影中线点(贴图 px);铭位锚/刻痕落点</summary>
        public static Vector2 SpinePx(float u) => OniBladeProfile.SpinePx(u);
        /// <summary>u 处刃缘点(贴图 px),standoff 向刃外让出;流光走这侧</summary>
        public static Vector2 EdgePx(float u, float standoff = 0f) => OniBladeProfile.EdgePx(u, standoff);
        /// <summary>u 处栋/柄缘点(贴图 px),standoff 向外让出;锚钉/引线搭点走这侧</summary>
        public static Vector2 BackPx(float u, float standoff = 0f) => OniBladeProfile.BackPx(u, standoff);
        /// <summary>u 处刃缘切向角(弧度,贴图与屏幕同向)</summary>
        public static float EdgeTangent(float u) => OniBladeProfile.EdgeTangent(u);

        /// <summary>
        /// 原生姿态绘制:originPx=贴图内锚(缩放的不动点),screenPos=锚的屏幕位;
        /// 剪影落影垫底,绯纹段透一层呼吸妖光;批内 Deferred+PointClamp 保像素锐利
        /// </summary>
        public static void Draw(SpriteBatch sb, Vector2 originPx, Vector2 screenPos, float scale,
            float alpha, float time) {
            Texture2D tex = OniBladeProfile.Texture;
            if (tex == null) {
                return;
            }

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
