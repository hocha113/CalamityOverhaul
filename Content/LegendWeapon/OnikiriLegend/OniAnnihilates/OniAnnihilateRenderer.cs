using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>灭世一闪渲染. ArcTech/BurstTech→OniAnnihilateArc</summary>
    internal static class OniAnnihilateRenderer
    {
        /// <summary>水墨旋钮,热核层只留散锋</summary>
        public struct InkParams
        {
            public float InkStep; //0..1 墨阶
            public float FeiBai; //0..1 飞白
            public float Bleed; //0..1 洇边
            public float SplitTail; //0..1 散锋
        }

        /// <summary>设备状态+公共 uniform,资产未就绪返 false</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniAnnihilateArc?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
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

        /// <summary>主体色带+白热薄条</summary>
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

        /// <summary>单层 ArcTech quad</summary>
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

        /// <summary>罡气舌. dissolve 0..1 根→尖,intensity 包络</summary>
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
