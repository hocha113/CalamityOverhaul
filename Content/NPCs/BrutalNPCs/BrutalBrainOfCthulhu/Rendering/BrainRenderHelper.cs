using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Rendering
{
    /// <summary>克脑本体/假体共用绘制</summary>
    internal static class BrainRenderHelper
    {
        /// <summary>确保贴图已加载并返回；失败返回 null</summary>
        public static Texture2D GetBrainTexture() {
            Main.instance.LoadNPC(NPCID.BrainofCthulhu);
            return TextureAssets.Npc[NPCID.BrainofCthulhu].Value;
        }

        /// <summary>由 0~7 帧号取帧矩形（竖排 8 帧）</summary>
        public static Rectangle GetFrameRect(Texture2D tex, int frame) {
            int frameHeight = tex.Height / Main.npcFrameCount[NPCID.BrainofCthulhu];
            return new Rectangle(0, frame * frameHeight, tex.Width, frameHeight);
        }

        /// <summary>
        /// 镜像质感身体：uGhost 溶解、uCold 冷偏（假体=1）
        /// shader 不可用时回退普通绘制
        /// </summary>
        public static void DrawBrainBody(SpriteBatch spriteBatch, Texture2D tex, Vector2 drawPos,
            Rectangle frameRect, Color lightColor, float rotation, float scale, SpriteEffects effects,
            float ghost, float cold, float alphaMul = 1f) {

            Vector2 origin = frameRect.Size() * 0.5f;
            Effect shader = EffectLoader.BrainMirrorImage?.Value;

            if (shader == null || (ghost <= 0.01f && cold <= 0.01f)) {
                //普通绘制（实体真身热路径）
                spriteBatch.Draw(tex, drawPos, frameRect, lightColor * alphaMul,
                    rotation, origin, scale, effects, 0f);
                return;
            }

            float invW = 1f / tex.Width;
            float invH = 1f / tex.Height;
            Vector4 frameUV = new Vector4(
                frameRect.X * invW, frameRect.Y * invH,
                frameRect.Width * invW, frameRect.Height * invH);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uGhost"]?.SetValue(MathHelper.Clamp(ghost, 0f, 1f));
            shader.Parameters["uCold"]?.SetValue(MathHelper.Clamp(cold, 0f, 1f));
            shader.Parameters["uFrameUV"]?.SetValue(frameUV);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成精灵贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, drawPos, frameRect, lightColor * alphaMul,
                rotation, origin, scale, effects, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>血色8向描边（预警节拍），加色</summary>
        public static void DrawBloodRim(SpriteBatch spriteBatch, Texture2D tex, Vector2 drawPos,
            Rectangle frameRect, float rotation, float scale, SpriteEffects effects, float strength) {
            if (strength <= 0.02f) {
                return;
            }
            Vector2 origin = frameRect.Size() * 0.5f;
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            Color rim = new Color(190, 26, 34, 0) * strength * pulse;
            float offset = 2f + 2.5f * strength;
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * offset;
                spriteBatch.Draw(tex, drawPos + dir, frameRect, rim, rotation, origin, scale, effects, 0f);
            }
        }

        /// <summary>心光底衬（真身独有，节拍收缩）</summary>
        public static void DrawHeartGlow(SpriteBatch spriteBatch, Vector2 drawPos, float baseScale, float pulse, float strength) {
            if (strength <= 0.02f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() * 0.5f;
            //收缩期骤亮骤缩：脉冲让光核先胀后缩
            float beatScale = 1f + pulse * 0.35f;
            Color inner = new Color(255, 92, 74, 0) * (0.55f * strength * (0.6f + pulse * 0.6f));
            Color outer = new Color(150, 20, 30, 0) * (0.4f * strength);
            spriteBatch.Draw(glow, drawPos, null, outer, 0f, origin, baseScale * 3.4f * beatScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, inner, 0f, origin, baseScale * 1.9f * beatScale, SpriteEffects.None, 0f);
        }

        /// <summary>眼芒：真身出手前兆（可学习破绽）</summary>
        public static void DrawEyeGlint(SpriteBatch spriteBatch, Vector2 drawPos, float strength, float rotation) {
            if (strength <= 0.02f) {
                return;
            }
            Texture2D flare = CWRAsset.StarFlare01.Value;
            Vector2 origin = flare.Size() * 0.5f;
            float flick = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            Color c = new Color(255, 214, 200, 0) * strength * flick;
            spriteBatch.Draw(flare, drawPos, null, c, rotation, origin, 0.34f * strength, SpriteEffects.None, 0f);
            spriteBatch.Draw(flare, drawPos, null, new Color(255, 120, 110, 0) * strength * 0.7f,
                rotation + 0.6f, origin, 0.55f * strength, SpriteEffects.None, 0f);
        }

        /// <summary>主控完整绘制入口</summary>
        public static void DrawBrain(SpriteBatch spriteBatch, NPC npc, BrainStateContext context,
            Vector2 screenPos, Color drawColor) {

            Texture2D tex = GetBrainTexture();
            if (tex == null) {
                return;
            }

            Rectangle frameRect = npc.frame;
            if (frameRect.Height <= 0) {
                frameRect = GetFrameRect(tex, 0);
            }
            Vector2 drawPos = npc.Center - screenPos;
            SpriteEffects effects = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //心光底衬：二阶段常亮，一阶段露心时透出
            float heartStrength = context.IsPhase2 ? 0.9f :
                (context.HeartExposed || context.FrameCommand == 1 ? 0.75f : context.ShellCrack * 0.8f);
            DrawHeartGlow(spriteBatch, drawPos, npc.scale, BrainHeartbeat.Pulse, heartStrength * context.GhostFade);

            //高速残影
            float speed = npc.velocity.Length();
            if (speed > 13f) {
                float trailAlpha = MathHelper.Clamp((speed - 13f) / 26f, 0f, 0.75f);
                for (int i = 2; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)npc.oldPos.Length;
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size * 0.5f - screenPos;
                    Color ghostColor = new Color(160, 24, 36, 0) * (trailAlpha * k * 0.55f);
                    spriteBatch.Draw(tex, ghostPos, frameRect, ghostColor, npc.rotation,
                        frameRect.Size() * 0.5f, npc.scale * (1f - (1f - k) * 0.08f), effects, 0f);
                }
            }

            //预警血环描边
            DrawBloodRim(spriteBatch, tex, drawPos, frameRect, npc.rotation, npc.scale, effects, context.TelegraphGlow);

            //本体：瞬移中虚影
            float ghost = 1f - context.GhostFade;
            DrawBrainBody(spriteBatch, tex, drawPos, frameRect, drawColor, npc.rotation, npc.scale,
                effects, ghost, 0f, MathHelper.Lerp(0.35f, 1f, context.GhostFade));

            //壳裂透光：闭壳下叠加开壳帧的加色低透，读作光从裂缝渗出
            if (context.ShellCrack > 0.02f && !context.IsPhase2) {
                int openFrame = 4 + (int)(Main.GlobalTimeWrappedHourly * 8f) % 4;
                Rectangle openRect = GetFrameRect(tex, openFrame);
                float crackPulse = 0.6f + 0.4f * BrainHeartbeat.Pulse;
                Color crackColor = new Color(255, 60, 50, 0) * (context.ShellCrack * 0.6f * crackPulse);
                spriteBatch.Draw(tex, drawPos, openRect, crackColor, npc.rotation,
                    openRect.Size() * 0.5f, npc.scale, effects, 0f);
            }

            //眼芒前兆
            DrawEyeGlint(spriteBatch, drawPos - Vector2.UnitY * 12f * npc.scale, context.EyeGlint, npc.rotation);
        }
    }
}
