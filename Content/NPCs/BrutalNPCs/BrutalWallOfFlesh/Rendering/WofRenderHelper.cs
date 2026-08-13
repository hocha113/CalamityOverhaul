using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering
{
    /// <summary>着色器面片绘制：墙体覆膜/口部漩涡/后方血幕，全部世界锚定</summary>
    internal static class WofRenderHelper
    {
        /// <summary>墙条带深度 px(与 WofFleshWall.fx 的分区一致)</summary>
        private const float WallDepth = 265f;
        /// <summary>拖曳肉髓长度 px</summary>
        private const float TrailLength = 1150f;
        /// <summary>面缘前伸 px</summary>
        private const float FaceBleed = 70f;

        /// <summary>切到 Immediate+LinearWrap 画一片世界矩形，再还原NPC批</summary>
        private static void DrawWorldQuad(SpriteBatch sb, Effect effect, BlendState blend, Rectangle worldRect) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, blend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            Texture2D quad = VaultAsset.placeholder2.Value;
            Vector2 drawPos = new Vector2(worldRect.X, worldRect.Y) - Main.screenPosition;
            Vector2 scale = new Vector2(worldRect.Width / (float)quad.Width, worldRect.Height / (float)quad.Height);
            sb.Draw(quad, drawPos, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>可见性裁剪：世界矩形与屏幕求交，全屏外跳过</summary>
        private static bool OnScreen(Rectangle worldRect) {
            Rectangle screen = new Rectangle((int)Main.screenPosition.X - 100, (int)Main.screenPosition.Y - 100,
                Main.screenWidth + 200, Main.screenHeight + 200);
            return worldRect.Intersects(screen);
        }

        #region 墙体覆膜

        /// <summary>血肉覆膜：墙条带蠕动+面缘热线+拖尾肉髓。在口器 Draw 内调用</summary>
        public static void DrawWallOverlay(SpriteBatch sb, NPC wall, WofStateContext ctx) {
            Effect effect = EffectLoader.WofFleshWall?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            float faceX = WofWallField.WallFaceX(wall);
            float top = WofWallField.Top - 70f;
            float bottom = WofWallField.Bottom + 70f;
            if (bottom - top < 80f) {
                return;
            }

            float xMin = wall.direction > 0 ? faceX - WallDepth - TrailLength : faceX - FaceBleed;
            float xMax = wall.direction > 0 ? faceX + FaceBleed : faceX + WallDepth + TrailLength;
            Rectangle worldRect = new Rectangle((int)xMin, (int)top, (int)(xMax - xMin), (int)(bottom - top));
            if (!OnScreen(worldRect)) {
                return;
            }

            effect.Parameters["uWorldRect"]?.SetValue(new Vector4(worldRect.X, worldRect.Y, worldRect.Width, worldRect.Height));
            effect.Parameters["uFaceX"]?.SetValue(faceX);
            effect.Parameters["uDir"]?.SetValue((float)wall.direction);
            effect.Parameters["uTop"]?.SetValue(WofWallField.Top);
            effect.Parameters["uBottom"]?.SetValue(WofWallField.Bottom);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFlush"]?.SetValue(MathHelper.Clamp(ctx.WallFlush, 0f, 1f));
            effect.Parameters["uCharge"]?.SetValue(ctx.ChargeType == 1 ? ctx.ChargeProgress : 0f);
            effect.Parameters["uOpacity"]?.SetValue(1f);
            effect.Parameters["uImage1"]?.SetValue(CWRAsset.PerlinNoise.Value);

            DrawWorldQuad(sb, effect, BlendState.AlphaBlend, worldRect);
        }

        #endregion

        #region 口部漩涡

        /// <summary>吸引漩涡面片，progress 展开、suck 流速</summary>
        public static void DrawMawVortex(SpriteBatch sb, NPC wall, float progress, float suck) {
            Effect effect = EffectLoader.WofMawVortex?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null || progress <= 0.01f) {
                return;
            }

            float size = 720f + 220f * progress;
            Rectangle worldRect = new Rectangle((int)(wall.Center.X - size / 2), (int)(wall.Center.Y - size / 2),
                (int)size, (int)size);
            if (!OnScreen(worldRect)) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uIntensity"]?.SetValue(1f);
            effect.Parameters["uSuck"]?.SetValue(MathHelper.Clamp(suck, 0f, 1f));
            effect.Parameters["uImage1"]?.SetValue(CWRAsset.PerlinNoise.Value);

            DrawWorldQuad(sb, effect, BlendState.AlphaBlend, worldRect);
        }

        #endregion

        #region 后方血幕

        /// <summary>大迁徙后方血幕，edgeX=前缘世界X，facingDir=前缘朝向口袋方向</summary>
        public static void DrawBloodCurtain(SpriteBatch sb, float edgeX, int facingDir, float intensity) {
            Effect effect = EffectLoader.WofBloodCurtain?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null || intensity <= 0.01f) {
                return;
            }

            const float CurtainDepth = 560f;
            float top = WofWallField.Top - 320f;
            float bottom = WofWallField.Bottom + 320f;
            float xMin = facingDir > 0 ? edgeX - CurtainDepth : edgeX - 90f;
            float xMax = facingDir > 0 ? edgeX + 90f : edgeX + CurtainDepth;
            Rectangle worldRect = new Rectangle((int)xMin, (int)top, (int)(xMax - xMin), (int)(bottom - top));
            if (!OnScreen(worldRect)) {
                return;
            }

            effect.Parameters["uWorldRect"]?.SetValue(new Vector4(worldRect.X, worldRect.Y, worldRect.Width, worldRect.Height));
            effect.Parameters["uEdgeX"]?.SetValue(edgeX);
            effect.Parameters["uDir"]?.SetValue((float)facingDir);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            effect.Parameters["uImage1"]?.SetValue(CWRAsset.PerlinNoise.Value);

            DrawWorldQuad(sb, effect, BlendState.AlphaBlend, worldRect);
        }

        #endregion

        #region 口器蓄能光晕(无着色器依赖)

        /// <summary>口器充能光晕+汇聚星闪，charge 0~1</summary>
        public static void DrawMouthCharge(NPC wall, float charge, Color theme) {
            if (charge <= 0.02f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = wall.Center - Main.screenPosition;
            float flicker = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 34f);

            Color glowCol = theme with { A = 0 };
            Main.EntitySpriteDraw(glow, pos, null, glowCol * (0.85f * charge), 0f, glow.Size() / 2f,
                (2.6f + charge * 3f) * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null, glowCol * (0.7f * charge), Main.GlobalTimeWrappedHourly * 2.4f,
                star.Size() / 2f, 0.5f + charge * 0.55f, SpriteEffects.None, 0);
        }

        #endregion
    }
}
