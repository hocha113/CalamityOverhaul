using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps
{
    /// <summary>神威流带. EffectLoader.OniKamuiFlow,多股子带</summary>
    internal static class OniKamuiFlowRenderer
    {
        /// <summary>子带静态定义（一次冲刺内不变，动态量走 DrawRibbon 参数）</summary>
        public struct RibbonDef
        {
            public float HalfWidth;   //半幅宽(px)

            public float PerpOffset;  //垂直路径的平行偏移(px)

            public float Seed;        //噪声相位

            public float FlowMul;     //流速倍率（子带各异 → 层间视差）

            public float TearAmp;     //轮廓撕裂幅度

            public float HeadBoost;   //头段白热中脊强度

            public float OpacityMul;  //相对整体的透明度

        }

        /// <summary>沿带噪声瓦片长度(px)、uLenScale = 路径长/此值，墨纹钉在世界空间</summary>
        private const float NoiseTilePx = 260f;

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniKamuiFlow?.Value;
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
            fx.Parameters["uColHot"]?.SetValue(CrimsonSlashRenderer.ColHot);
            fx.Parameters["uColBright"]?.SetValue(CrimsonSlashRenderer.ColBright);
            fx.Parameters["uColDeep"]?.SetValue(CrimsonSlashRenderer.ColDeep);
            fx.Parameters["uColDark"]?.SetValue(CrimsonSlashRenderer.ColDark);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>绘制一股子带。points 首元素=起点(尾)</summary>
        public static void DrawRibbon(GraphicsDevice device, Effect fx
            , IReadOnlyList<Vector2> rawPoints, in RibbonDef def
            , float retract, float flash, float opacity) {
            if (rawPoints.Count < 2) {
                return;
            }

            //整形、剔短段 → 切角圆滑 → 细分到 ≤44px

            List<Vector2> points = ShapePath(rawPoints);
            if (points.Count < 2) {
                return;
            }
            int count = points.Count;

            float totalLen = 0f;
            for (int i = 1; i < count; i++) {
                totalLen += Vector2.Distance(points[i - 1], points[i]);
            }
            if (totalLen < 12f) {
                return;
            }

            float a = opacity * def.OpacityMul;
            if (a <= 0.01f) {
                return;
            }

            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(a, 0f, 1f));
            fx.Parameters["uRetract"]?.SetValue(MathHelper.Clamp(retract, 0f, 1f));
            fx.Parameters["uLenScale"]?.SetValue(totalLen / NoiseTilePx);
            fx.Parameters["uSeed"]?.SetValue(def.Seed);
            fx.Parameters["uFlowMul"]?.SetValue(def.FlowMul);
            fx.Parameters["uTearAmp"]?.SetValue(def.TearAmp);
            fx.Parameters["uHeadBoost"]?.SetValue(def.HeadBoost);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1.2f));

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[count * 2];
            float cum = 0f;
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    cum += Vector2.Distance(points[i - 1], points[i]);
                }
                float u = cum / totalLen;

                //切向取邻段平均，端点用单侧

                Vector2 dir = i == 0
                    ? points[1] - points[0]
                    : i == count - 1
                        ? points[i] - points[i - 1]
                        : points[i + 1] - points[i - 1];
                dir = dir.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                //幅宽包络、梭形：尾端从近零长出、最宽落在头侧（头端收尖由 shader 彗星鼻 taper 负责）

                float tailGrow = MathHelper.Clamp(u / 0.14f, 0f, 1f);
                tailGrow *= 2f - tailGrow;   //easeOut
                float hw = def.HalfWidth * MathHelper.Lerp(0.68f, 1f, u) * tailGrow;
                //平行偏移双向漏斗：头段归零汇入收束点，尾段收到三成、四股闭合成叶形但保留撕裂散口

                float funnel = MathHelper.Clamp((1f - u) / 0.34f, 0f, 1f);
                funnel = funnel * (2f - funnel);   //easeOut，汇入平滑无折角

                float tailFunnel = MathHelper.Clamp(u / 0.20f, 0f, 1f);
                tailFunnel = MathHelper.Lerp(0.30f, 1f, tailFunnel * (2f - tailFunnel));

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

        /// <summary>剔除阈值(px)、短于此的段其切向已是噪声，垂直挤出必然自折</summary>
        private const float MinSeg = 10f;

        /// <summary>路径整形三步、极端情况（向脚下急停的顶点簇、转向/台阶造成的弯折）的破损兜底</summary>
        private static List<Vector2> ShapePath(IReadOnlyList<Vector2> raw) {
            //1. 剔短段（末点承载头端语义、距离不足时顶替前点而不是丢弃）

            List<Vector2> culled = new(raw.Count);
            culled.Add(raw[0]);
            for (int i = 1; i < raw.Count; i++) {
                if (Vector2.DistanceSquared(culled[^1], raw[i]) >= MinSeg * MinSeg) {
                    culled.Add(raw[i]);
                }
                else if (i == raw.Count - 1) {
                    if (culled.Count > 1) {
                        culled[^1] = raw[i];
                    }
                    else {
                        culled.Add(raw[i]);
                    }
                }
            }
            if (culled.Count < 3) {
                return SubdividePath(culled);
            }

            //2. Chaikin 两轮切角

            List<Vector2> smooth = culled;
            for (int round = 0; round < 2; round++) {
                List<Vector2> next = new(smooth.Count * 2) { smooth[0] };
                for (int i = 0; i < smooth.Count - 1; i++) {
                    next.Add(Vector2.Lerp(smooth[i], smooth[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(smooth[i], smooth[i + 1], 0.75f));
                }
                next.Add(smooth[^1]);
                smooth = next;
            }

            //3. 补密

            return SubdividePath(smooth);
        }

        /// <summary>路径细分、任何超过 44px 的段插入等分点（原点集不变，仅补密）</summary>
        private static List<Vector2> SubdividePath(IReadOnlyList<Vector2> raw) {
            const float MaxSeg = 44f;
            if (raw.Count < 2) {
                return [.. raw];
            }
            List<Vector2> outPts = new(raw.Count * 4);
            outPts.Add(raw[0]);
            for (int i = 1; i < raw.Count; i++) {
                Vector2 a = raw[i - 1];
                Vector2 b = raw[i];
                float len = Vector2.Distance(a, b);
                int cuts = (int)(len / MaxSeg);
                for (int k = 1; k <= cuts; k++) {
                    outPts.Add(Vector2.Lerp(a, b, k / (float)(cuts + 1)));
                }
                outPts.Add(b);
            }
            return outPts;
        }

        /// <summary>按弧长比例取路径上一点（0=尾 1=头），供蒸发前沿定位烟尘</summary>
        public static Vector2 PointAlong(IReadOnlyList<Vector2> points, float t) {
            int count = points.Count;
            if (count == 0) {
                return Vector2.Zero;
            }
            if (count == 1 || t <= 0f) {
                return points[0];
            }
            if (t >= 1f) {
                return points[count - 1];
            }

            float totalLen = 0f;
            for (int i = 1; i < count; i++) {
                totalLen += Vector2.Distance(points[i - 1], points[i]);
            }
            float goal = totalLen * t;
            float cum = 0f;
            for (int i = 1; i < count; i++) {
                float seg = Vector2.Distance(points[i - 1], points[i]);
                if (cum + seg >= goal && seg > 0f) {
                    return Vector2.Lerp(points[i - 1], points[i], (goal - cum) / seg);
                }
                cum += seg;
            }
            return points[count - 1];
        }
    }
}
