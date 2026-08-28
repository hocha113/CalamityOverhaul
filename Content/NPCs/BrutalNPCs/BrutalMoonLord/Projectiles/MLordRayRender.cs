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
        /// <summary>
        /// 画一根死光柱。origin=光源点，宽长为视觉值，quad 自带回渗遮住硬边。
        /// rootWidthRatio=根宽/束身宽（&lt;1 时近源段收窄成喇叭，月明湮灭增幅期用；1=全束等宽）
        /// </summary>
        public static void DrawBeam(Vector2 origin, float angle, float length, float width, float opacity, float seed,
            float rootWidthRatio = 1f) {
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
            //每次绘制都显式置根部收窄量：参数驻留 Effect 实例，不恒置会串到其他射线上
            effect.Parameters["rootPinch"]?.SetValue(1f - MathHelper.Clamp(rootWidthRatio, 0.05f, 1f));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            //合同：噪声显式钉在 s1（与 .fx 的 register(s1) 对应），不依赖参数隐式绑定
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 按所在批次折算染色：A=0 是 AlphaBlend（One/InvSrcAlpha）批的加色惯用法；
        /// Additive 批（SrcBlend=SourceAlpha）里 A=0 会把源色整体乘零画不出来，须保留实 alpha
        /// </summary>
        private static Color BatchTint(Color color, bool additiveBatch) {
            return additiveBatch ? color : color with { A = 0 };
        }

        /// <summary>
        /// 无着色器退避：三层嵌套光带走 LightShot 灰度（尖端自带羽化不留平切口），
        /// 根部由源点光核收口。仅从图元层调用，彼时无活动 SpriteBatch，须自起自收
        /// </summary>
        private static void DrawFallback(Vector2 origin, float angle, float length, float width, float opacity) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 screenOrigin = origin - Main.screenPosition;
            DrawStreak(streak, screenOrigin, angle, length * 1.04f, width * 2.2f, MLordDirector.DeepViolet * (0.5f * opacity), false);
            DrawStreak(streak, screenOrigin, angle, length, width * 1.1f, MLordDirector.Phantasmal * (0.85f * opacity), false);
            DrawStreak(streak, screenOrigin, angle, length * 0.96f, width * 0.34f, MLordDirector.MoonWhite * opacity, false);
            DrawMuzzle(origin, width / 60f, opacity, additiveBatch: false);
            Main.spriteBatch.End();
        }

        private static void DrawStreak(Texture2D streak, Vector2 screenOrigin, float angle, float length, float width,
            Color color, bool additiveBatch) {
            Vector2 scale = new(length / streak.Width, width / streak.Height);
            Main.EntitySpriteDraw(streak, screenOrigin, null, BatchTint(color, additiveBatch), angle,
                new Vector2(0f, streak.Height * 0.5f), scale, SpriteEffects.None, 0);
        }

        /// <summary>光源口部呼吸辉团 + 星芒（各射线共用装饰），additiveBatch=所在批次为 Additive</summary>
        public static void DrawMuzzle(Vector2 origin, float widthRatio, float opacity, bool additiveBatch) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 screenPos = origin - Main.screenPosition;
            float flicker = 1f + 0.09f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 34f);
            Main.EntitySpriteDraw(glow, screenPos, null, BatchTint(MLordDirector.DeepViolet, additiveBatch) * (0.8f * opacity), 0f,
                glow.Size() / 2f, widthRatio * 1.9f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, BatchTint(MLordDirector.Phantasmal, additiveBatch) * opacity, 0f,
                glow.Size() / 2f, widthRatio * 1.15f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, BatchTint(MLordDirector.MoonWhite, additiveBatch) * (0.9f * opacity),
                Main.GlobalTimeWrappedHourly * 2.6f, star.Size() / 2f, widthRatio * 0.62f * flicker, SpriteEffects.None, 0);
        }

        /// <summary>细预警线（雕出即将成束的位置）：LightShot 尖端羽化 + 源点光核收根口，additiveBatch=所在批次为 Additive</summary>
        public static void DrawGuideLine(Vector2 origin, float angle, float length, float strength, bool additiveBatch = false) {
            if (strength <= 0.01f) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (streak == null || glow == null) {
                return;
            }
            Vector2 screenOrigin = origin - Main.screenPosition;
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            DrawStreak(streak, screenOrigin, angle, length, 22f, MLordDirector.DeepViolet * (0.4f * strength * pulse), additiveBatch);
            DrawStreak(streak, screenOrigin, angle, length * 0.97f, 8f, MLordDirector.Phantasmal * (0.9f * strength * pulse), additiveBatch);
            //源点光核：预警线自光源"长"出来，根部无裸切边
            Main.EntitySpriteDraw(glow, screenOrigin, null,
                BatchTint(MLordDirector.Phantasmal, additiveBatch) * (0.8f * strength * pulse), 0f,
                glow.Size() / 2f, 0.34f + 0.3f * strength, SpriteEffects.None, 0);
        }
    }
}
