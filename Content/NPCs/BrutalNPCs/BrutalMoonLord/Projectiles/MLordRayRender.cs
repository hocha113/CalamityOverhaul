using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>幻影死光共用绘制：MLordDeathray.fx 星质光柱 + 无着色器三层退避</summary>
    internal static class MLordRayRender
    {
        /// <summary>画一根死光柱。origin=光源点，宽长为视觉值，quad 自带回渗遮住硬边</summary>
        public static void DrawBeam(Vector2 origin, float angle, float length, float width, float opacity, float seed) {
            if (width <= 0.5f || length <= 8f || opacity <= 0.01f) {
                return;
            }

            Effect effect = EffectLoader.MLordDeathray?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                DrawFallback(origin, angle, length, width, opacity);
                return;
            }

            Vector2 dir = angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //近端回渗，把硬切边埋进光源体内
            Vector2 root = origin - dir * (width * 0.4f + 26f);
            Vector2 tip = origin + dir * length;
            //半宽放大容纳引力缘光
            float halfW = width * 2.6f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((root + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((root - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["seed"]?.SetValue(seed);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>无着色器退避：三层嵌套加色条（宽紫/中青/细月白）</summary>
        private static void DrawFallback(Vector2 origin, float angle, float length, float width, float opacity) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 screenOrigin = origin - Main.screenPosition;
            DrawStrip(pixel, screenOrigin, angle, length, width * 2.2f, MLordDirector.DeepViolet * (0.5f * opacity));
            DrawStrip(pixel, screenOrigin, angle, length, width * 1.1f, MLordDirector.Phantasmal * (0.85f * opacity));
            DrawStrip(pixel, screenOrigin, angle, length, width * 0.34f, MLordDirector.MoonWhite * opacity);
        }

        private static void DrawStrip(Texture2D pixel, Vector2 screenOrigin, float angle, float length, float width, Color color) {
            Vector2 scale = new(length / pixel.Width, width / pixel.Height);
            Main.EntitySpriteDraw(pixel, screenOrigin, null, color with { A = 0 }, angle,
                new Vector2(0f, pixel.Height * 0.5f), scale, SpriteEffects.None, 0);
        }

        /// <summary>光源口部呼吸辉团 + 星芒（各射线共用装饰）</summary>
        public static void DrawMuzzle(Vector2 origin, float widthRatio, float opacity) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 screenPos = origin - Main.screenPosition;
            float flicker = 1f + 0.09f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 34f);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.DeepViolet with { A = 0 } * (0.8f * opacity), 0f,
                glow.Size() / 2f, widthRatio * 1.9f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * opacity, 0f,
                glow.Size() / 2f, widthRatio * 1.15f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * opacity),
                Main.GlobalTimeWrappedHourly * 2.6f, star.Size() / 2f, widthRatio * 0.62f * flicker, SpriteEffects.None, 0);
        }

        /// <summary>细预警线（雕出即将成束的位置）</summary>
        public static void DrawGuideLine(Vector2 origin, float angle, float length, float strength) {
            if (strength <= 0.01f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 screenOrigin = origin - Main.screenPosition;
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            DrawStrip(pixel, screenOrigin, angle, length, 5f, MLordDirector.DeepViolet * (0.4f * strength * pulse));
            DrawStrip(pixel, screenOrigin, angle, length, 1.6f, MLordDirector.Phantasmal * (0.9f * strength * pulse));
        }
    }
}
