using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering
{
    /// <summary>皇后本体绘制：复刻原版翼/宝石/凝胶体/王冠管线，叠加形变与虹彩</summary>
    internal static class QueenSlimeRenderHelper
    {
        /// <summary>王冠世界锚点(光束发射口)，与帧偏移解耦保证联机一致</summary>
        public static Vector2 CrownAnchor(NPC npc) => npc.Top - new Vector2(0f, 26f);

        /// <summary>宝石帧偏移表(原版)</summary>
        private static float GemOffset(int frame) => frame switch {
            1 or 6 => -10f,
            3 or 5 => 10f,
            4 or 12 or 13 or 14 or 15 => 18f,
            7 or 8 => -14f,
            9 => -16f,
            10 => -18f,
            11 => 20f,
            20 => -14f,
            21 or 23 => -18f,
            22 => -22f,
            _ => 0f,
        };

        /// <summary>王冠帧偏移表(原版)</summary>
        private static float CrownOffset(int frame) => frame switch {
            1 => -10f,
            3 or 5 or 6 => 10f,
            4 or 12 or 13 or 14 or 15 => 18f,
            7 or 8 => -14f,
            9 => -16f,
            10 => -18f,
            11 => 20f,
            20 => -14f,
            21 or 23 => -18f,
            22 => -22f,
            _ => 0f,
        };

        public static void DrawFull(SpriteBatch sb, NPC npc, QueenSlimeStateContext ctx, Vector2 screenPos, Color drawColor) {
            Texture2D bodyTex = TextureAssets.Npc[npc.type].Value;
            int frame = ctx.BodyFrame;
            Rectangle bodyRect = bodyTex.Frame(2, 16, frame / 16, frame % 16);
            bodyRect.Inflate(0, -2);
            Vector2 bodyOrigin = bodyRect.Size() * new Vector2(0.5f, 1f);
            Vector2 bodyPos = npc.Bottom - screenPos + new Vector2(0f, 2f);
            Color halfLit = Color.Lerp(Color.White, drawColor, 0.5f);
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            bool dummy = npc.IsABestiaryIconDummy;

            //形变(底边锚定)：正=纵向拉伸 负=压扁
            float pulse = MathHelper.Clamp(ctx.SquashPulse, -0.55f, 0.75f);
            Vector2 squashScale = new Vector2(1f - pulse * 0.32f, 1f + pulse * 0.45f) * npc.scale;

            //翅膀(身后)
            if (ctx.WingSpread > 0.01f) {
                DrawWings(sb, npc, ctx, screenPos, npc.GetAlpha(halfLit));
            }

            //核心宝石(凝胶体内透出)
            DrawGem(sb, npc, ctx, screenPos, npc.GetAlpha(halfLit), fx);

            //凝胶体：残影+本体走原版 QueenSlime 凝胶着色器
            if (!dummy) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            GameShaders.Misc["QueenSlime"].Apply();

            //高速残影(冲刺/俯冲)，凝胶质感鬼影
            if (ctx.AfterimageBoost > 0.05f && npc.oldPos.Length > 0) {
                int ghostCount = Math.Min(8, npc.oldPos.Length);
                for (int n = ghostCount - 1; n >= 1; n--) {
                    Vector2 ghostPos = npc.oldPos[n] + new Vector2(npc.width * 0.5f, npc.height) - screenPos + new Vector2(0f, 2f);
                    float fade = (1f - n / (float)ghostCount) * 0.4f * ctx.AfterimageBoost;
                    sb.Draw(bodyTex, ghostPos, bodyRect, npc.GetAlpha(halfLit) * fade,
                        npc.rotation, bodyOrigin, npc.scale, fx, 0f);
                }
            }

            //本体
            DrawData bodyData = new DrawData(bodyTex, bodyPos, bodyRect, npc.GetAlpha(halfLit),
                npc.rotation, bodyOrigin, squashScale, fx);
            GameShaders.Misc["QueenSlime"].Apply(bodyData);
            bodyData.Draw(sb);

            //加色层：晶面皮肤(着色器)/虹彩泛光/蓄力辉光/翼光痕
            if (!dummy) {
                sb.End();

                //晶面皮肤：分面镶嵌+折射闪点+色散缘光，蓄力/冲刺时点亮
                float skinIntensity = MathHelper.Clamp(ctx.PrismShimmer
                    + (ctx.IsCharging ? ctx.ChargeProgress * 0.5f : 0f), 0f, 1f);
                bool skinDrawn = TryDrawCrystalSkin(sb, npc, bodyTex, bodyRect, bodyPos, bodyOrigin, squashScale, fx, skinIntensity);

                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                DrawAdditiveOverlays(sb, npc, ctx, bodyTex, bodyRect, bodyPos, bodyOrigin, squashScale, fx, skinDrawn);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //王冠(最上层)
            DrawCrown(sb, npc, ctx, screenPos, npc.GetAlpha(halfLit), fx);
        }

        private static void DrawWings(SpriteBatch sb, NPC npc, QueenSlimeStateContext ctx, Vector2 screenPos, Color color) {
            Texture2D wingTex = TextureAssets.Extra[185].Value;
            int wingFrame = ctx.WingFrameCounter / 6 % 4;
            Rectangle wingRect = wingTex.Frame(1, 4, 0, wingFrame);
            float spread = QueenMotion.SnapOut(ctx.WingSpread, 3);
            float wingScale = 0.8f * (0.3f + 0.7f * spread);
            Color wingColor = color * spread;

            for (int i = 0; i < 2; i++) {
                float originX = 1f;
                float offsetX = 0f;
                SpriteEffects wfx = SpriteEffects.None;
                if (i == 1) {
                    originX = 0f;
                    offsetX = 2f;
                    wfx = SpriteEffects.FlipHorizontally;
                }
                Vector2 origin = wingRect.Size() * new Vector2(originX, 0.5f);
                Vector2 pos = new Vector2(npc.Center.X + offsetX, npc.Center.Y);
                if (npc.rotation != 0f) {
                    pos = pos.RotatedBy(npc.rotation, npc.Bottom);
                }
                pos -= screenPos;
                float tilt = MathHelper.Clamp(npc.velocity.Y, -6f, 6f) * -0.1f;
                if (i == 0) {
                    tilt *= -1f;
                }
                sb.Draw(wingTex, pos, wingRect, wingColor, npc.rotation + tilt, origin, wingScale, wfx, 0f);
            }
        }

        private static void DrawGem(SpriteBatch sb, NPC npc, QueenSlimeStateContext ctx, Vector2 screenPos, Color color, SpriteEffects fx) {
            Texture2D gemTex = TextureAssets.Extra[186].Value;
            Rectangle gemRect = gemTex.Frame();
            Vector2 origin = gemRect.Size() * 0.5f;
            Vector2 pos = new Vector2(npc.Center.X, npc.Center.Y + GemOffset(ctx.BodyFrame));
            if (npc.rotation != 0f) {
                pos = pos.RotatedBy(npc.rotation, npc.Bottom);
            }
            pos -= screenPos;
            sb.Draw(gemTex, pos, gemRect, color, npc.rotation, origin, 1f, fx, 0f);
        }

        private static void DrawCrown(SpriteBatch sb, NPC npc, QueenSlimeStateContext ctx, Vector2 screenPos, Color color, SpriteEffects fx) {
            Texture2D crownTex = TextureAssets.Extra[177].Value;
            Rectangle crownRect = crownTex.Frame();
            Vector2 origin = crownRect.Size() * 0.5f;
            Vector2 pos = new Vector2(npc.Center.X, npc.Top.Y - crownRect.Bottom + 44f + CrownOffset(ctx.BodyFrame));
            if (npc.rotation != 0f) {
                pos = pos.RotatedBy(npc.rotation, npc.Bottom);
            }
            pos -= screenPos;
            sb.Draw(crownTex, pos, crownRect, color, npc.rotation, origin, 1f, fx, 0f);
        }

        /// <summary>晶面皮肤(着色器加色层)：以本体帧为画布叠水晶质感；返回是否成功走了着色器</summary>
        private static bool TryDrawCrystalSkin(SpriteBatch sb, NPC npc, Texture2D bodyTex, Rectangle bodyRect,
            Vector2 bodyPos, Vector2 bodyOrigin, Vector2 squashScale, SpriteEffects fx, float intensity) {
            Effect effect = EffectLoader.QueenCrystalSkin?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || intensity <= 0.02f) {
                return effect != null && noise != null;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uHueSeed"]?.SetValue(npc.whoAmI * 0.17f % 1f);
            effect.Parameters["uUvRect"]?.SetValue(new Vector4(
                bodyRect.X / (float)bodyTex.Width, bodyRect.Y / (float)bodyTex.Height,
                bodyRect.Width / (float)bodyTex.Width, bodyRect.Height / (float)bodyTex.Height));
            effect.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / bodyTex.Width, 1f / bodyTex.Height));

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1(shader 内 register(s1))
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(bodyTex, bodyPos, bodyRect, Color.White, npc.rotation, bodyOrigin, squashScale, fx, 0f);
            sb.End();
            return true;
        }

        private static void DrawAdditiveOverlays(SpriteBatch sb, NPC npc, QueenSlimeStateContext ctx,
            Texture2D bodyTex, Rectangle bodyRect, Vector2 bodyPos, Vector2 bodyOrigin, Vector2 squashScale, SpriteEffects fx,
            bool skinDrawn) {
            float time = Main.GlobalTimeWrappedHourly;

            //虹彩泛光：着色器缺席时的体表色相流转回退(晶面皮肤已画则跳过)
            float shimmer = MathHelper.Clamp(ctx.PrismShimmer + (ctx.IsCharging ? ctx.ChargeProgress * 0.5f : 0f), 0f, 1f);
            if (!skinDrawn && shimmer > 0.02f) {
                Color hue = QueenMotion.PrismHue(time * 0.45f);
                sb.Draw(bodyTex, bodyPos, bodyRect, hue * (0.42f * shimmer), npc.rotation,
                    bodyOrigin, squashScale * 1.015f, fx, 0f);
                Color hue2 = QueenMotion.PrismHue(time * 0.45f + 0.33f);
                sb.Draw(bodyTex, bodyPos, bodyRect, hue2 * (0.2f * shimmer), npc.rotation,
                    bodyOrigin, squashScale * 1.05f, fx, 0f);
            }

            //蓄力王冠辉光
            if (ctx.IsCharging && ctx.ChargeProgress > 0.03f) {
                Texture2D star = CWRAsset.StarTexture.Value;
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 crownPos = CrownAnchor(npc) - Main.screenPosition;
                float p = ctx.ChargeProgress;
                float flick = 1f + 0.12f * (float)Math.Sin(time * 34f);
                Color hue = QueenMotion.PrismHue(time * 0.6f);
                sb.Draw(glow, crownPos, null, hue * (0.75f * p), 0f, glow.Size() / 2f, 2.6f * p * flick, SpriteEffects.None, 0f);
                sb.Draw(glow, crownPos, null, Color.White * (0.5f * p), 0f, glow.Size() / 2f, 1.2f * p, SpriteEffects.None, 0f);
                sb.Draw(star, crownPos, null, hue * (0.9f * p), time * 2.6f, star.Size() / 2f, 0.5f * p * flick, SpriteEffects.None, 0f);
                sb.Draw(star, crownPos, null, Color.White * (0.65f * p), -time * 1.8f, star.Size() / 2f, 0.3f * p, SpriteEffects.None, 0f);
            }

            //高速翼光痕
            if (ctx.WingSpread > 0.5f && ctx.WingFlapBoost > 0.25f) {
                Texture2D wingTex = TextureAssets.Extra[185].Value;
                int wingFrame = ctx.WingFrameCounter / 6 % 4;
                Rectangle wingRect = wingTex.Frame(1, 4, 0, wingFrame);
                Color hue = QueenMotion.PrismHue(time * 0.5f + 0.15f) * (0.5f * ctx.WingFlapBoost);
                for (int i = 0; i < 2; i++) {
                    float originX = i == 0 ? 1f : 0f;
                    SpriteEffects wfx = i == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    Vector2 origin = wingRect.Size() * new Vector2(originX, 0.5f);
                    Vector2 pos = npc.Center - npc.velocity * 0.6f - Main.screenPosition;
                    sb.Draw(wingTex, pos, wingRect, hue, npc.rotation, origin, 0.86f, wfx, 0f);
                }
            }
        }
    }
}
