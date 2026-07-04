using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFlashSteps
{
    /// <summary>
    /// 神威流带渲染：沿冲刺路径铺三角带，交给 <see cref="EffectLoader.OniKamuiFlow"/>
    /// 画成流动的黑红墨绸。<br/>
    /// 一条路径叠多股子带（垂直偏移 + 各自的种子/流速/撕裂度）构成"多股平行流带"的
    /// 手绘观感；白热主脊由 HeadBoost 大的窄带承担。调色板与绯红裂空斩共享
    /// （<see cref="CrimsonSlashRenderer"/> 四色），形态语言完全独立
    /// </summary>
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

        /// <summary>沿带噪声瓦片长度(px)：uLenScale = 路径长/此值，墨纹钉在世界空间</summary>
        private const float NoiseTilePx = 260f;

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniKamuiFlow?.Value;
            Texture2D noise = OnikiriAssets.NoiseSoft01?.Value;
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

        /// <summary>
        /// 绘制一股子带。points 首元素=起点(尾)、末元素=头端；
        /// retract 0..1 从尾向头蒸发，flash 全形过曝帧，opacity 整体包络
        /// </summary>
        public static void DrawRibbon(GraphicsDevice device, Effect fx
            , IReadOnlyList<Vector2> points, in RibbonDef def
            , float retract, float flash, float opacity) {
            int count = points.Count;
            if (count < 2) {
                return;
            }

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

                //幅宽包络：尾端略收、头端全宽（撕裂舌由 shader 负责，几何只给画布）
                float hw = def.HalfWidth * MathHelper.Lerp(0.68f, 1f, u);
                Vector2 center = points[i] + perp * def.PerpOffset;

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
