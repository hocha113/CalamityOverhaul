using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights
{
    /// <summary>
    /// 樱流的顶点层. EffectLoader.OniSakuraFlow:
    /// TechStream 航线流带(多股子带), TechCoreBloom 花核瓣盘.
    /// 路径整形复用 <see cref="OniKamuiFlowRenderer.ShapePath"/>(与材质无关)
    /// </summary>
    internal static class OniSakuraFlowRenderer
    {
        /// <summary>子带静态定义（一次飞行内不变，动态量走 DrawStream 参数）</summary>
        public struct StreamDef
        {
            public float HalfWidth;   //半幅宽(px)

            public float PerpOffset;  //垂直航线的平行偏移(px)

            public float Seed;        //噪声相位

            public float FlowMul;     //流速倍率（子带各异 → 层间视差）

            public float GrainAmp;    //瓣粒分明度（越大孔越多、越碎）

            public float HeadBoost;   //头段瓣白中脊强度

            public float OpacityMul;  //相对整体的透明度

        }

        //==== 调色(瓣白热/亮樱/深绯/墨绯底/和纸黄) ====
        //自 OniSakuraFlight 的瓣色 (178,48,79)/(229,90,119)/(255,243,247) 与残心樱衣提取，
        //档位语义沿用 CrimsonSlashRenderer，色相移到樱
        public static readonly Vector3 ColHot = new(1.55f, 1.30f, 1.34f);
        public static readonly Vector3 ColBright = new(1.28f, 0.42f, 0.58f);
        public static readonly Vector3 ColDeep = new(0.62f, 0.14f, 0.24f);
        //尾色不沉到近黑:瓣的尾迹靠透明变稀，墨才靠压黑
        public static readonly Vector3 ColDark = new(0.24f, 0.065f, 0.105f);
        public static readonly Vector3 ColWashi = new(0.95f, 0.82f, 0.56f);

        /// <summary>沿带噪声瓦片长度(px)、uLenScale = 航线长/此值，瓣纹钉在世界空间</summary>
        private const float NoiseTilePx = 240f;

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniSakuraFlow?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBright"]?.SetValue(ColBright);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColDark"]?.SetValue(ColDark);
            fx.Parameters["uColWashi"]?.SetValue(ColWashi);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>绘制一股流带。points 首元素=尾(最旧), 末元素=头(花核所在)</summary>
        public static void DrawStream(GraphicsDevice device, Effect fx
            , IReadOnlyList<Vector2> rawPoints, in StreamDef def
            , float retract, float flash, float opacity) {
            if (rawPoints.Count < 2) {
                return;
            }

            List<Vector2> points = OniKamuiFlowRenderer.ShapePath(rawPoints);
            if (points.Count < 2) {
                return;
            }
            int count = points.Count;

            float totalLen = 0f;
            for (int i = 1; i < count; i++) {
                totalLen += Vector2.Distance(points[i - 1], points[i]);
            }
            if (totalLen < 14f) {
                return;
            }

            float a = opacity * def.OpacityMul;
            if (a <= 0.01f) {
                return;
            }

            fx.CurrentTechnique = fx.Techniques["TechStream"];
            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(a, 0f, 1f));
            fx.Parameters["uRetract"]?.SetValue(MathHelper.Clamp(retract, 0f, 1f));
            fx.Parameters["uLenScale"]?.SetValue(totalLen / NoiseTilePx);
            fx.Parameters["uSeed"]?.SetValue(def.Seed);
            fx.Parameters["uFlowMul"]?.SetValue(def.FlowMul);
            fx.Parameters["uGrainAmp"]?.SetValue(def.GrainAmp);
            fx.Parameters["uHeadBoost"]?.SetValue(def.HeadBoost);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1.2f));

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[count * 2];
            float cum = 0f;
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    cum += Vector2.Distance(points[i - 1], points[i]);
                }
                float u = cum / totalLen;

                Vector2 dir = i == 0
                    ? points[1] - points[0]
                    : i == count - 1
                        ? points[i] - points[i - 1]
                        : points[i + 1] - points[i - 1];
                dir = dir.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                //幅宽包络：尾端从近零长出、最宽落在头侧（头端钝尖由 shader 收束负责）
                float tailGrow = MathHelper.Clamp(u / 0.16f, 0f, 1f);
                tailGrow *= 2f - tailGrow;   //easeOut
                float hw = def.HalfWidth * MathHelper.Lerp(0.72f, 1f, u) * tailGrow;

                //平行偏移双向漏斗：头段归零汇进花核，尾段收到三成、多股闭合成叶形
                float funnel = MathHelper.Clamp((1f - u) / 0.32f, 0f, 1f);
                funnel = funnel * (2f - funnel);

                float tailFunnel = MathHelper.Clamp(u / 0.22f, 0f, 1f);
                tailFunnel = MathHelper.Lerp(0.32f, 1f, tailFunnel * (2f - tailFunnel));

                Vector2 center = points[i] + perp * (def.PerpOffset * funnel * tailFunnel);

                verts[i * 2] = new VertexPositionColorTexture(
                    (center - perp * hw).ToVector3(), Color.White, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    (center + perp * hw).ToVector3(), Color.White, new Vector2(u, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, count * 2 - 2);
            }
        }

        /// <summary>
        /// 螺旋花涡一枚。quad 轴对齐不旋转，拉长在 shader 内沿 axis 做,
        /// 故两轴半幅都给 radius*stretch，横向由 shader 压回;
        /// seed 喂涡内瓣粒噪声的相位，不同层错开
        /// </summary>
        public static void DrawCore(GraphicsDevice device, Effect fx, Vector2 center
            , float radius, Vector2 axis, float stretch, float spin, float seed
            , Color color, float bloom, float heartHeat, float opacity) {
            if (radius <= 1f || opacity <= 0.004f) {
                return;
            }

            stretch = MathHelper.Clamp(stretch, 1f, 3f);
            axis = axis.SafeNormalize(Vector2.UnitX);

            fx.CurrentTechnique = fx.Techniques["TechCoreBloom"];
            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            fx.Parameters["uSpin"]?.SetValue(spin);
            fx.Parameters["uStretch"]?.SetValue(stretch);
            fx.Parameters["uAxis"]?.SetValue(axis);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uBloom"]?.SetValue(bloom);
            fx.Parameters["uHeartHeat"]?.SetValue(heartHeat);

            float h = radius * stretch;
            Vector3 tl = new(center.X - h, center.Y - h, 0f);
            Vector3 tr = new(center.X + h, center.Y - h, 0f);
            Vector3 bl = new(center.X - h, center.Y + h, 0f);
            Vector3 br = new(center.X + h, center.Y + h, 0f);

            VertexPositionColorTexture[] verts =
            [
                new(tl, color, new Vector2(0f, 0f)),
                new(tr, color, new Vector2(1f, 0f)),
                new(bl, color, new Vector2(0f, 1f)),
                new(br, color, new Vector2(1f, 1f)),
            ];

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
