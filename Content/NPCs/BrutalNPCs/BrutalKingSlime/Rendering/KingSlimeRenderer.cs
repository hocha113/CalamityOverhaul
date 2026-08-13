using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering
{
    /// <summary>本体绘制：体内忍者→皇家凝胶身体(RoyalAura shader)→挤压拉伸弹簧形变</summary>
    internal static class KingSlimeRenderer
    {
        /// <summary>每帧推进压扁弹簧与摇晃衰减(各端本地)</summary>
        public static void UpdateSpring(KingSlimeStateContext ctx) {
            //弹簧回中
            ctx.SquashVelocity += (1f - ctx.VisualSquash) * 0.16f;
            ctx.SquashVelocity *= 0.8f;
            ctx.VisualSquash += ctx.SquashVelocity;
            ctx.VisualSquash = MathHelper.Clamp(ctx.VisualSquash, 0.28f, 1.9f);

            //摇晃衰减
            ctx.WobblePhase += 0.32f;
            ctx.WobbleAmp *= 0.93f;
            if (ctx.WobbleAmp < 0.004f) {
                ctx.WobbleAmp = 0f;
            }
        }

        /// <summary>身体绘制入口，返回false=已接管</summary>
        public static void DrawBody(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos, Color drawColor) {
            if (ctx.HideBodySprite || ctx.BodyOpacity <= 0.01f) {
                return;
            }

            Texture2D bodyTex = TextureAssets.Npc[npc.type].Value;
            int frameCount = Main.npcFrameCount[npc.type];
            Rectangle frameRec = npc.frame;
            if (frameRec.Height <= 0) {
                frameRec = bodyTex.GetRectangle(0, frameCount);
            }

            //形变：压扁变宽、拉伸变窄，近似体积守恒
            float squash = ctx.VisualSquash;
            float wobble = ctx.WobbleAmp;
            float wobbleX = 1f + (float)Math.Sin(ctx.WobblePhase) * wobble;
            float wobbleY = 1f - (float)Math.Sin(ctx.WobblePhase + 1.1f) * wobble * 0.8f;
            float scaleY = npc.scale * squash * wobbleY;
            float scaleX = npc.scale * (1f + (1f - squash) * 0.85f) * wobbleX;

            //锚定底部：压扁时贴地不悬空
            Vector2 bottom = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos + new Vector2(0f, npc.gfxOffY + 4f);
            Vector2 origin = new Vector2(frameRec.Width * 0.5f, frameRec.Height);
            //立塔倾倒角，绕底部中心
            float lean = ctx.BodyLean;

            float opacity = ctx.BodyOpacity;
            Color bodyColor = drawColor * opacity;

            //---------------- 体内忍者 ----------------
            if (!ctx.NinjaGone) {
                DrawNinja(spriteBatch, npc, ctx, screenPos, drawColor, opacity, scaleX, scaleY, lean);
            }

            //---------------- 皇家凝胶身体 ----------------
            Effect aura = EffectLoader.KingSlimeRoyalAura?.Value;
            bool shaderOn = aura != null && opacity > 0.05f;
            if (shaderOn) {
                aura.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                aura.Parameters["intensity"]?.SetValue(MathHelper.Clamp(0.55f + ctx.AuraProgress * 0.45f, 0f, 1f) * opacity);
                aura.Parameters["mode"]?.SetValue((float)ctx.AuraMode);
                aura.Parameters["progress"]?.SetValue(ctx.AuraProgress);
                aura.Parameters["texelSize"]?.SetValue(new Vector2(1f / bodyTex.Width, 1f / bodyTex.Height));
                aura.Parameters["seed"]?.SetValue(npc.whoAmI * 0.173f % 1f);
                aura.Parameters["royalCore"]?.SetValue(KingSlimeGelFX.CrownGold.ToVector3());
                aura.Parameters["royalEdge"]?.SetValue(new Vector3(0.42f, 0.5f, 1f));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                aura.CurrentTechnique.Passes[0].Apply();
            }

            SpriteEffects flip = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(bodyTex, bottom, frameRec, bodyColor, lean,
                origin, new Vector2(scaleX, scaleY), flip, 0f);

            if (shaderOn) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //蓄力/狂暴时体表加一圈微弱加色轮廓光
            if (ctx.AuraProgress > 0.35f) {
                Color glow = KingSlimeGelFX.GelFoam with { A = 0 };
                spriteBatch.Draw(bodyTex, bottom, frameRec, glow * ((ctx.AuraProgress - 0.35f) * 0.3f * opacity), lean,
                    origin, new Vector2(scaleX * 1.03f, scaleY * 1.03f), flip, 0f);
            }

            //入场王冠天降(纯演出层)
            DrawIntroCrownDrop(spriteBatch, npc, ctx, screenPos);
        }

        /// <summary>入场演出：王冠从天而降扣上头顶，加速下落+金色残影</summary>
        private static void DrawIntroCrownDrop(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos) {
            float t = ctx.IntroCrownDrop;
            if (t <= 0f || t > 1f) {
                return;
            }
            Main.instance.LoadGore(Terraria.ID.GoreID.KingSlimeCrown);
            Texture2D crown = TextureAssets.Gore[Terraria.ID.GoreID.KingSlimeCrown].Value;
            //加速坠落：ease-in二次
            float fall = t * t;
            Vector2 dest = npc.Top + new Vector2(0f, -10f);
            Vector2 pos = dest - new Vector2(0f, (1f - fall) * 620f) - screenPos;
            Vector2 origin = crown.Size() * 0.5f;

            //金色坠落残影
            for (int i = 1; i <= 3; i++) {
                Vector2 ghost = pos - new Vector2(0f, i * 26f * t);
                spriteBatch.Draw(crown, ghost, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.3f - i * 0.08f),
                    0f, origin, 1f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(crown, pos, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * 0.4f, 0f, origin, 1.04f, SpriteEffects.None, 0f);
        }

        /// <summary>体内忍者：速度滞后漂移，影袭前摇发亮</summary>
        private static void DrawNinja(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos,
            Color drawColor, float opacity, float scaleX, float scaleY, float lean) {
            Texture2D ninja = TextureAssets.Ninja.Value;
            //滞后：身体动、忍者拖
            Vector2 lag = new Vector2(-npc.velocity.X * 2f, -npc.velocity.Y);
            //压扁时忍者被压低
            float squashDrop = (1f - MathHelper.Clamp(ctx.VisualSquash, 0.3f, 1f)) * npc.height * 0.3f;
            lag.Y += squashDrop;
            //限制在体内
            float maxLag = 24f * npc.scale;
            if (lag.Length() > maxLag) {
                lag = lag.SafeNormalize(Vector2.Zero) * maxLag;
            }

            Vector2 pos = npc.Center - screenPos + lag + new Vector2(0f, npc.gfxOffY);
            //随倾倒角绕底部中心旋转
            if (lean != 0f) {
                Vector2 pivot = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos;
                pos = pivot + (pos - pivot).RotatedBy(lean);
            }
            float rot = npc.velocity.X * 0.05f + lean;
            Rectangle rec = new Rectangle(0, 0, ninja.Width, ninja.Height);
            Vector2 origin = rec.Size() * 0.5f;

            spriteBatch.Draw(ninja, pos, rec, drawColor * (opacity * 0.9f), rot, origin, 1f, SpriteEffects.None, 0f);

            //影袭前摇：忍者剪影亮起冷白
            if (ctx.NinjaGlow > 0.01f) {
                Color glow = new Color(200, 226, 255, 0) * ctx.NinjaGlow * opacity;
                spriteBatch.Draw(ninja, pos, rec, glow, rot, origin, 1.04f, SpriteEffects.None, 0f);
                float flicker = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
                spriteBatch.Draw(ninja, pos, rec, glow * (0.5f * flicker), rot, origin, 1.12f, SpriteEffects.None, 0f);
            }
        }
    }
}
