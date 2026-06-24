using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 比目鱼技能共用的手感与图元工具：镜头冲击、GradientTrail 参数装配、加色三角带圆环（顶点绘制）
    /// 让各鱼技能在"力量感运动 + 着色器拖尾 + 顶点冲击波"上保持统一语言
    /// </summary>
    internal static class FishSkillVFX
    {
        /// <summary>
        /// 取最大值的镜头冲击；仅本地玩家、且服务器配置开启屏幕震动时生效，避免多端各自抖动与配置越权
        /// </summary>
        public static void Punch(Player owner, float amount) {
            if (owner == null || owner.whoAmI != Main.myPlayer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            CWRPlayer modPlayer = owner.GetModPlayer<CWRPlayer>();
            modPlayer.ScreenShakeValue = MathHelper.Max(modPlayer.ScreenShakeValue, amount);
        }

        /// <summary>
        /// GradientTrail 标准参数装配；调用方负责设置 <see cref="BlendState"/> 后再 DrawTrail
        /// </summary>
        public static void ApplyGradientTrail(Effect effect, Texture2D gradientBar, Texture2D baseImage, float flowSpeed = 0.08f) {
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * flowSpeed);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(baseImage);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(gradientBar);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);
        }

        /// <summary>
        /// 加色三角带圆环（真正的顶点绘制）。须在外部已 Begin 的 Immediate/Additive 批次中调用，
        /// 由该批次为设备绑定精灵着色器；颜色由内/外环顶点插值，<paramref name="squash"/> 做地面透视压扁
        /// </summary>
        public static void DrawShockRing(Texture2D tex, Vector2 screenCenter, float radius, float thickness
            , Color innerColor, Color outerColor, int segments = 72, float squash = 1f, float rot = 0f
            , float jitter = 0f, float jitterPhase = 0f, float jitterFreq = 6f) {
            if (radius <= 1f || thickness <= 0.1f || segments < 3) {
                return;
            }

            int vertCount = (segments + 1) * 2;
            ColoredVertex[] verts = new ColoredVertex[vertCount];
            float half = thickness * 0.5f;

            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                float ang = t * MathHelper.TwoPi + rot;
                Vector2 dir = ang.ToRotationVector2();
                dir.Y *= squash;

                float r = radius;
                if (jitter > 0f) {
                    r += (float)Math.Sin(ang * jitterFreq + jitterPhase) * jitter;
                }

                verts[i * 2] = new ColoredVertex(screenCenter + dir * (r - half), innerColor, new Vector3(t, 0f, 1f));
                verts[i * 2 + 1] = new ColoredVertex(screenCenter + dir * (r + half), outerColor, new Vector3(t, 1f, 1f));
            }

            Main.graphics.GraphicsDevice.Textures[0] = tex;
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
        }

        /// <summary>
        /// 沿路径展开的加色三角带飘带（顶点绘制）。<paramref name="screenPoints"/> 为屏幕坐标采样点，
        /// 宽度/颜色按沿程参数 t（0=头, 1=尾）取值，可做逐顶点彩虹等任意配色。
        /// 须在外部已 Begin 的 Immediate/Additive 批次内调用。
        /// </summary>
        public static void DrawRibbon(Texture2D tex, IReadOnlyList<Vector2> screenPoints
            , Func<float, float> widthFunc, Func<float, Color> colorFunc) {
            if (screenPoints == null || screenPoints.Count < 2) {
                return;
            }

            int n = screenPoints.Count;
            ColoredVertex[] verts = new ColoredVertex[n * 2];
            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                Vector2 dir;
                if (i == 0) {
                    dir = screenPoints[1] - screenPoints[0];
                }
                else if (i == n - 1) {
                    dir = screenPoints[n - 1] - screenPoints[n - 2];
                }
                else {
                    dir = screenPoints[i + 1] - screenPoints[i - 1];
                }

                Vector2 normal = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float w = widthFunc(t) * 0.5f;
                Color c = colorFunc(t);
                verts[i * 2] = new ColoredVertex(screenPoints[i] + normal * w, c, new Vector3(t, 0f, 1f));
                verts[i * 2 + 1] = new ColoredVertex(screenPoints[i] - normal * w, c, new Vector3(t, 1f, 1f));
            }

            Main.graphics.GraphicsDevice.Textures[0] = tex;
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
        }

        /// <summary>
        /// 可复用的顶点冲击波环：随生命扩张、变薄、淡出，可压扁成贴地椭圆。
        /// 由弹幕维护实例列表，AI 内 <see cref="Update"/>，绘制时在 Immediate/Additive 批次内 <see cref="Draw"/>。
        /// </summary>
        public sealed class ShockRing
        {
            private readonly Vector2 center;
            private readonly float maxRadius;
            private readonly float baseThickness;
            private readonly Color color;
            private readonly float squash;
            private readonly int segments;
            private readonly float phase;
            private readonly float edgeFade;
            private int life;
            private readonly int maxLife;

            public bool Dead => life >= maxLife;

            public ShockRing(Vector2 center, float maxRadius, float thickness, Color color
                , float squash = 1f, int maxLife = 26, int segments = 72, float edgeFade = 0.15f) {
                this.center = center;
                this.maxRadius = maxRadius;
                baseThickness = thickness;
                this.color = color;
                this.squash = squash;
                this.maxLife = maxLife;
                this.segments = segments;
                this.edgeFade = edgeFade;
                phase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public void Update() => life++;

            public void Draw(Texture2D tex) {
                float p = life / (float)maxLife;
                float radius = VaultUtils.EaseOutCubic(p) * maxRadius;
                float alpha = (float)Math.Sin((1f - p) * MathHelper.PiOver2);
                float thickness = baseThickness * (1.4f - p);
                Color inner = color * alpha;
                inner.A = 0;
                Color outer = color * (alpha * edgeFade);
                outer.A = 0;
                DrawShockRing(tex, center - Main.screenPosition, radius, thickness, inner, outer
                    , segments, squash, 0f, radius * 0.04f, phase + life * 0.2f);
            }
        }
    }
}
