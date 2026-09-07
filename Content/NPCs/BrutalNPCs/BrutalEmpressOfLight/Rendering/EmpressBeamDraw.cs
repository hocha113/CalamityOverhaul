using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>光束类图元绘制：标记带（EmpressBeamMarker）与束体（EmpressSunbeam），加色批，四顶点条带</summary>
    internal static class EmpressBeamDraw
    {
        private static readonly VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];

        /// <summary>宽软预告标记；hitFrac=真实命中半宽/视觉半宽</summary>
        public static void DrawMarker(Vector2 start, Vector2 dir, float length, float halfWidth, Color color,
            float opacity, float focus, float glow, float breath, float hitFrac) {
            Effect effect = EffectLoader.EmpressBeamMarker?.Value;
            if (effect == null || opacity <= 0.005f) {
                return;
            }
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uOpacity"]?.SetValue(opacity);
            effect.Parameters["uFocus"]?.SetValue(MathHelper.Clamp(focus, 0f, 1f));
            effect.Parameters["uGlow"]?.SetValue(glow);
            effect.Parameters["uBreath"]?.SetValue(MathHelper.Clamp(breath, 0f, 1f));
            effect.Parameters["uColor"]?.SetValue(color.ToVector3());
            effect.Parameters["uHitFrac"]?.SetValue(MathHelper.Clamp(hitFrac, 0.02f, 1f));
            DrawStrip(effect, start, dir, length, halfWidth);
        }

        /// <summary>正式束体（EmpressSunbeam）：hue 夜光谱；昼把 tint 传金白由 VertexColor 乘</summary>
        public static void DrawSunbeam(Vector2 start, Vector2 dir, float length, float halfWidth, float hue,
            float widthRatio, Color tint) {
            Effect effect = EffectLoader.EmpressSunbeam?.Value;
            if (effect == null) {
                return;
            }
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHue"]?.SetValue(hue);
            effect.Parameters["uTelegraph"]?.SetValue(0f);
            effect.Parameters["uWidthRatio"]?.SetValue(MathHelper.Clamp(widthRatio, 0.03f, 1f));
            DrawStrip(effect, start, dir, length, halfWidth, tint);
        }

        /// <summary>楔形标记：起端窄、末端宽（熔光扇射线）</summary>
        public static void DrawMarkerTaper(Vector2 start, Vector2 dir, float length, float halfStart, float halfEnd, Color color,
            float opacity, float focus, float glow, float breath, float hitFrac) {
            Effect effect = EffectLoader.EmpressBeamMarker?.Value;
            if (effect == null || opacity <= 0.005f) {
                return;
            }
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uOpacity"]?.SetValue(opacity);
            effect.Parameters["uFocus"]?.SetValue(MathHelper.Clamp(focus, 0f, 1f));
            effect.Parameters["uGlow"]?.SetValue(glow);
            effect.Parameters["uBreath"]?.SetValue(MathHelper.Clamp(breath, 0f, 1f));
            effect.Parameters["uColor"]?.SetValue(color.ToVector3());
            effect.Parameters["uHitFrac"]?.SetValue(MathHelper.Clamp(hitFrac, 0.02f, 1f));
            DrawStrip(effect, start, dir, length, halfStart, null, halfEnd);
        }

        /// <summary>楔形束体（熔光扇发射）</summary>
        public static void DrawSunbeamTaper(Vector2 start, Vector2 dir, float length, float halfStart, float halfEnd, float hue,
            float widthRatio, Color tint) {
            Effect effect = EffectLoader.EmpressSunbeam?.Value;
            if (effect == null) {
                return;
            }
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHue"]?.SetValue(hue);
            effect.Parameters["uTelegraph"]?.SetValue(0f);
            effect.Parameters["uWidthRatio"]?.SetValue(MathHelper.Clamp(widthRatio, 0.03f, 1f));
            DrawStrip(effect, start, dir, length, halfStart, tint, halfEnd);
        }

        private static void DrawStrip(Effect effect, Vector2 start, Vector2 dir, float length, float halfWidth, Color? tint = null, float? halfEnd = null) {
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            Color c = tint ?? Color.White;
            float endHalf = halfEnd ?? halfWidth;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 end = start + dir * length;
            quad[0] = new VertexPositionColorTexture((start + perp * halfWidth).ToVector3(), c, new Vector2(0f, 0f));
            quad[1] = new VertexPositionColorTexture((start - perp * halfWidth).ToVector3(), c, new Vector2(0f, 1f));
            quad[2] = new VertexPositionColorTexture((end + perp * endHalf).ToVector3(), c, new Vector2(1f, 0f));
            quad[3] = new VertexPositionColorTexture((end - perp * endHalf).ToVector3(), c, new Vector2(1f, 1f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
