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
            //噪声显式绑到 s1（四张 Wof 面片 shader 均声明 uImage1:register(s1)）：
            //SpriteBatch.Draw 只覆写 s0，参数式贴图绑定实机不可靠（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

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

            //深侧退场包络在 560px 归零，噪声撕裂最深 -37px——quad 多给 90px 让包络先于边界闭合
            const float CurtainDepth = 650f;
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

            DrawWorldQuad(sb, effect, BlendState.AlphaBlend, worldRect);
        }

        #endregion

        #region 墙后尸山血海

        /// <summary>
        /// 尸山血海背景层：墙体碾过的后方是凝血海面+尸山剪影+升腾血雾。
        /// 纯视觉零判定，quad裁剪到屏幕可见部分(图案世界/视差锚定，裁剪不位移)。
        /// 在口器 Draw 最先调用——垫在覆膜/口器/弹幕之下
        /// </summary>
        public static void DrawBloodSea(SpriteBatch sb, NPC wall, float intensity) {
            if (intensity <= 0.01f) {
                return;
            }

            float faceX = WofWallField.WallFaceX(wall);
            int dir = wall.direction >= 0 ? 1 : -1;
            //前缘藏在墙体条带背后，与拖尾覆膜重叠过渡
            float edgeX = faceX - dir * 305f;

            float scrL = Main.screenPosition.X - 60f;
            float scrR = Main.screenPosition.X + Main.screenWidth + 60f;
            float xMin, xMax;
            if (dir > 0) {
                xMin = scrL;
                xMax = Math.Min(edgeX + 140f, scrR);
            }
            else {
                xMin = Math.Max(edgeX - 140f, scrL);
                xMax = scrR;
            }
            if (xMax - xMin < 12f) {
                return;
            }
            float top = Main.screenPosition.Y - 60f;
            float bottom = Main.screenPosition.Y + Main.screenHeight + 60f;
            Rectangle worldRect = new Rectangle((int)xMin, (int)top, (int)(xMax - xMin), (int)(bottom - top));

            //海平面：墙域中下部，海体淹没走廊底
            float surfaceY = MathHelper.Lerp(WofWallField.Top, WofWallField.Bottom, 0.58f);

            Effect effect = EffectLoader.WofBloodSea?.Value;
            if (effect == null || CWRAsset.PerlinNoise?.Value == null) {
                DrawBloodSeaFallback(sb, worldRect, surfaceY, intensity);
                return;
            }

            effect.Parameters["uWorldRect"]?.SetValue(new Vector4(worldRect.X, worldRect.Y, worldRect.Width, worldRect.Height));
            effect.Parameters["uEdgeX"]?.SetValue(edgeX);
            effect.Parameters["uDir"]?.SetValue((float)dir);
            effect.Parameters["uSurfaceY"]?.SetValue(surfaceY);
            effect.Parameters["uScreenX"]?.SetValue(Main.screenPosition.X);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));

            DrawWorldQuad(sb, effect, BlendState.AlphaBlend, worldRect);
        }

        /// <summary>着色器缺失回退：三段渐变血幕(雾带+海面亮带+海体)，保证不隐形</summary>
        private static void DrawBloodSeaFallback(SpriteBatch sb, Rectangle worldRect, float surfaceY, float intensity) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 origin = Vector2.Zero;
            float seaTop = MathHelper.Clamp(surfaceY, worldRect.Y, worldRect.Bottom);

            //海面上：暗红雾
            float hazeH = seaTop - worldRect.Y;
            if (hazeH > 2f) {
                sb.Draw(px, new Vector2(worldRect.X, worldRect.Y) - Main.screenPosition, null,
                    new Color(46, 8, 12) * (0.30f * intensity), 0f, origin,
                    new Vector2(worldRect.Width / (float)px.Width, hazeH / px.Height), SpriteEffects.None, 0f);
            }
            //海面亮带
            sb.Draw(px, new Vector2(worldRect.X, seaTop - 6f) - Main.screenPosition, null,
                new Color(150, 30, 22) * (0.5f * intensity), 0f, origin,
                new Vector2(worldRect.Width / (float)px.Width, 12f / px.Height), SpriteEffects.None, 0f);
            //海体
            float seaH = worldRect.Bottom - seaTop;
            if (seaH > 2f) {
                sb.Draw(px, new Vector2(worldRect.X, seaTop) - Main.screenPosition, null,
                    new Color(52, 7, 10) * (0.62f * intensity), 0f, origin,
                    new Vector2(worldRect.Width / (float)px.Width, seaH / px.Height), SpriteEffects.None, 0f);
            }
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
