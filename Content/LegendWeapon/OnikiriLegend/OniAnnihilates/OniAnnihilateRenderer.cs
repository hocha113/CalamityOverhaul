using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>
    /// 灭世一闪专属渲染：水墨巨弧（ArcTech）与泼墨罡气舌（BurstTech）交给
    /// <see cref="EffectLoader.OniAnnihilateArc"/>。<br/>
    /// 几何/生命周期复用终之太刀的 <see cref="OFR.BladeDef"/>/<see cref="OFR.BladeState"/>
    /// 数据结构与标准生命周期，绘制端换成带水墨旋钮（墨阶/飞白/洇边/散锋）的专属
    /// uniform 集 —— 终之太刀共用代码零改动
    /// </summary>
    internal static class OniAnnihilateRenderer
    {
        /// <summary>水墨旋钮组（逐层传入，热核薄条关掉墨阶/飞白/洇边只留散锋）</summary>
        public struct InkParams
        {
            public float InkStep;   //0..1 墨分五色（密度阶化 + 积墨线）
            public float FeiBai;    //0..1 飞白干笔断丝
            public float Bleed;     //0..1 洇边外渗进度
            public float SplitTail; //0..1 散锋分叉
        }

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniAnnihilateArc?.Value;
            Texture2D brush = OnikiriAssets.SlashBrush01?.Value;
            Texture2D noise = OnikiriAssets.NoiseSoft01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || brush == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uBrushTex"]?.SetValue(brush);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>双层异步结构：水墨主体色带 + 白热核心薄条（墨相细节关闭、只留散锋，
        /// 剃刀线在收笔端一同分叉成锋毫）</summary>
        public static void DrawBladeLayers(GraphicsDevice device, Effect fx, in OFR.BladeDef d
            , in OFR.BladeState s, Vector2 center, in InkParams ink) {
            DrawBlade(device, fx, in d, in s, center, in ink
                , opacityMul: 1f, thickMul: 1f, frontMul: 1f, forceHot: false);

            OFR.BladeState core = s;
            core.ColorShift = 0f;
            core.Opacity = s.Opacity * 0.92f;
            InkParams coreInk = new() { InkStep = 0f, FeiBai = 0f, Bleed = 0f, SplitTail = ink.SplitTail };
            DrawBlade(device, fx, in d, in core, center, in coreInk
                , opacityMul: 1f, thickMul: 0.42f, frontMul: 1.25f, forceHot: true);
        }

        /// <summary>单层绘制：以 (def, state, ink) 提交 quad，调色取自 def.Palette</summary>
        public static void DrawBlade(GraphicsDevice device, Effect fx, in OFR.BladeDef d
            , in OFR.BladeState s, Vector2 center, in InkParams ink
            , float opacityMul, float thickMul, float frontMul, bool forceHot) {
            float opacity = s.Opacity * opacityMul;
            if (opacity <= 0.012f) {
                return;
            }
            fx.CurrentTechnique = fx.Techniques["ArcTech"];

            Vector2 axisX = (d.Rot + s.RotOffset).ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * s.ScaleMul;
            float hy = d.HalfY * s.ScaleMul;

            fx.Parameters["uSweep"]?.SetValue(s.Sweep);
            fx.Parameters["uErode"]?.SetValue(MathHelper.Clamp(s.Erode, 0f, 1f));
            fx.Parameters["uTailErode"]?.SetValue(s.TailErode);
            fx.Parameters["uFlash"]?.SetValue(s.Flash);
            fx.Parameters["uFlowPhase"]?.SetValue(s.FlowPhase);
            fx.Parameters["uColorShift"]?.SetValue(forceHot ? 0f : s.ColorShift);
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uFlip"]?.SetValue(d.Flip);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uArcSpan"]?.SetValue(d.Span > 0f ? d.Span : 1f);
            fx.Parameters["uThick"]?.SetValue(d.Thick * s.ThickMul * thickMul);
            fx.Parameters["uFrontGlow"]?.SetValue(s.FrontGlow * frontMul);
            fx.Parameters["uRazorTailWiden"]?.SetValue(d.RazorTailWiden);
            fx.Parameters["uInkStep"]?.SetValue(ink.InkStep);
            fx.Parameters["uFeiBai"]?.SetValue(ink.FeiBai);
            fx.Parameters["uBleed"]?.SetValue(ink.Bleed);
            fx.Parameters["uSplitTail"]?.SetValue(ink.SplitTail);
            ApplyPalette(fx, in d.Palette);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>
        /// 一条泼墨罡气舌：root 为根部（玩家中心），沿 angle 方向伸出 length。<br/>
        /// dissolve 0..1 从根向尖散掉，intensity 整体强度包络
        /// </summary>
        public static void DrawTongue(GraphicsDevice device, Effect fx
            , Vector2 root, float angle, float length, float halfWidth
            , float seed, float dissolve, float intensity, float opacity) {
            if (length < 4f || opacity <= 0.012f || intensity <= 0.012f) {
                return;
            }
            fx.CurrentTechnique = fx.Techniques["BurstTech"];

            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve, 0f, 1f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1.2f));
            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            ApplyPalette(fx, OFR.BladePalette.Crimson);

            Vector2 dir = angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * halfWidth;
            Vector2 tip = root + dir * length;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((root - perp).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((tip - perp).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((root + perp).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((tip + perp).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        private static void ApplyPalette(Effect fx, in OFR.BladePalette palette) {
            fx.Parameters["uColHot"]?.SetValue(palette.Hot);
            fx.Parameters["uColBright"]?.SetValue(palette.Bright);
            fx.Parameters["uColDeep"]?.SetValue(palette.Deep);
            fx.Parameters["uColDark"]?.SetValue(palette.Dark);
        }
    }
}
