using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering
{
    /// <summary>石巨人渲染辅助：岩浆脉络/宝石充能/崩解侵蚀/拳残影</summary>
    internal static class GolemRenderHelper
    {
        /// <summary>岩浆脉络覆盖层：贴体采样身体贴图，脉络亮度随 VeinGlow</summary>
        internal static void DrawMagmaVeins(SpriteBatch sb, NPC npc, GolemStateContext ctx) {
            float glow = ctx?.VeinGlow ?? 0f;
            if (glow < 0.03f) {
                return;
            }
            Effect shader = EffectLoader.GolemMagmaVein?.Value;
            if (shader == null) {
                //兜底：宝石处热光
                Texture2D soft = CWRAsset.SoftGlow.Value;
                Vector2 gemPos = npc.Center + new Vector2(0f, -6f) - Main.screenPosition;
                sb.Draw(soft, gemPos, null, new Color(255, 160, 60, 0) * (0.5f * glow),
                    0f, soft.Size() / 2f, 0.8f + 0.3f * glow, SpriteEffects.None, 0f);
                return;
            }

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = npc.Center - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique = shader.Techniques["VeinTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uGlow"]?.SetValue(glow);
            shader.Parameters["uCrumble"]?.SetValue(0f);
            //帧区域归一（防串帧）
            shader.Parameters["uFrame"]?.SetValue(new Vector4(
                frame.X / (float)body.Width, frame.Y / (float)body.Height,
                frame.Width / (float)body.Width, frame.Height / (float)body.Height));
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>宝石蓄力：旋涡吸积 + 核心亮斑</summary>
        internal static void DrawGemCharge(SpriteBatch sb, NPC npc, GolemStateContext ctx) {
            float progress = ctx.ChargeProgress;
            Vector2 gemPos = npc.Center + new Vector2(0f, -6f) - Main.screenPosition;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D cyclone = CWRAsset.Cyclone.Value;

            Color main = ctx.ChargeType >= 2 ? new Color(255, 150, 40) : new Color(255, 200, 90);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //旋涡吸积盘
            float spin = Main.GlobalTimeWrappedHourly * (2.2f + progress * 3f);
            float size = 0.5f + progress * 0.9f;
            sb.Draw(cyclone, gemPos, null, (main with { A = 0 }) * (0.5f * progress),
                spin, cyclone.Size() / 2f, size, SpriteEffects.None, 0f);
            sb.Draw(cyclone, gemPos, null, (Color.White with { A = 0 }) * (0.3f * progress),
                -spin * 0.7f, cyclone.Size() / 2f, size * 0.6f, SpriteEffects.None, 0f);
            //核心亮斑
            sb.Draw(soft, gemPos, null, (main with { A = 0 }) * (0.4f + 0.6f * progress),
                0f, soft.Size() / 2f, 0.5f + progress * 0.7f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>死亡演出：崩解侵蚀绘制（接管主绘制）</summary>
        internal static void DrawBodyCrumble(SpriteBatch sb, NPC npc, GolemStateContext ctx, Vector2 screenPos, Color drawColor) {
            int deathTimer = ctx?.DeathTimer ?? 0;
            float crumble = GolemDeathState.GetCrumble(deathTimer);

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos;

            Effect shader = EffectLoader.GolemMagmaVein?.Value;
            if (shader != null && crumble < 0.999f) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                shader.CurrentTechnique = shader.Techniques["CrumbleTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uGlow"]?.SetValue(1f);
                shader.Parameters["uCrumble"]?.SetValue(crumble);
                shader.Parameters["uFrame"]?.SetValue(new Vector4(
                    frame.X / (float)body.Width, frame.Y / (float)body.Height,
                    frame.Width / (float)body.Width, frame.Height / (float)body.Height));
                shader.Parameters["uNoise"]?.SetValue(CWRAsset.PerlinNoise.Value);
                shader.Parameters["uColor"]?.SetValue(drawColor.ToVector4());
                shader.CurrentTechnique.Passes[0].Apply();

                sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

                //存留区叠满强度岩浆脉络（与侵蚀线同步遮罩）
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                shader.CurrentTechnique = shader.Techniques["VeinTech"];
                shader.Parameters["uGlow"]?.SetValue(1f);
                shader.Parameters["uCrumble"]?.SetValue(crumble);
                shader.CurrentTechnique.Passes[0].Apply();
                sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else if (crumble < 0.999f) {
                //兜底：整体透明化
                sb.Draw(body, drawPos, frame, drawColor * (1f - crumble),
                    npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
            }

            //宝石谢幕层
            if (ctx != null && ctx.DeathPhase == GolemDeathPhase.GemFinale) {
                DrawGemFinale(sb, npc, deathTimer, screenPos);
            }
        }

        /// <summary>宝石谢幕：太阳宝石浮出废墟，碎响间隙闪烁</summary>
        private static void DrawGemFinale(SpriteBatch sb, NPC npc, int deathTimer, Vector2 screenPos) {
            Vector2 gemPos = GolemDeathState.GolemRenderHelperGemPos(npc, deathTimer) - screenPos;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;

            //终爆后不再绘制
            if (deathTimer >= 326) {
                return;
            }

            float t = MathHelper.Clamp((deathTimer - GolemDeathState.CollapseEnd) / 20f, 0f, 1f);
            //碎响帧白闪
            float crackFlash = 0f;
            if (deathTimer is >= 292 and < 296 || deathTimer is >= 307 and < 311 || deathTimer is >= 318 and < 322) {
                crackFlash = 1f;
            }
            float pulse = 0.8f + 0.2f * (float)Math.Sin(deathTimer * 0.3f);

            Color gold = new Color(255, 200, 90, 0);
            sb.Draw(soft, gemPos, null, gold * (0.85f * t * pulse),
                0f, soft.Size() / 2f, 0.9f + crackFlash * 0.4f, SpriteEffects.None, 0f);
            sb.Draw(soft, gemPos, null, (Color.White with { A = 0 }) * (0.7f * t),
                0f, soft.Size() / 2f, 0.4f + crackFlash * 0.25f, SpriteEffects.None, 0f);
            sb.Draw(star, gemPos, null, gold * (0.8f * t * pulse),
                deathTimer * 0.02f, star.Size() / 2f, 0.16f + crackFlash * 0.08f, SpriteEffects.None, 0f);
        }

        /// <summary>高速残影（速度门控，通用于拳/飞头）；overrideSpeed≥0 时代替实时速度</summary>
        internal static void DrawFistTrail(SpriteBatch sb, NPC npc, Vector2 screenPos, float overrideSpeed = -1f) {
            float speed = overrideSpeed >= 0f ? overrideSpeed : npc.velocity.Length();
            float heat = MathHelper.Clamp((speed - 13f) / 22f, 0f, 1f);
            if (heat <= 0.05f || npc.oldPos.Length == 0) {
                return;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = new Rectangle(0, 0, tex.Width, Math.Max(tex.Height / Math.Max(Main.npcFrameCount[npc.type], 1), 1));
            }
            Vector2 origin = frame.Size() / 2f;

            float alpha = 0.36f * heat;
            for (int i = 1; i < npc.oldPos.Length; i += 2) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                Color trailColor = Color.Lerp(new Color(255, 170, 70, 0), new Color(140, 60, 20, 0), i / (float)npc.oldPos.Length);
                sb.Draw(tex, drawOldPos, frame, trailColor * alpha,
                    npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                alpha *= 0.82f;
            }
        }

        /// <summary>拳蓄力辉光：汇聚亮斑 + 星芒（末段收缩）</summary>
        internal static void DrawFistWindup(SpriteBatch sb, NPC npc, GolemFistStateContext ctx) {
            float glow = ctx.WindupGlow;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = npc.Center - Main.screenPosition;

            //爆发前收缩：越满越小越亮
            float shrink = MathHelper.Lerp(1.15f, 0.62f, glow);
            Color gold = new Color(255, 190, 80, 0);
            sb.Draw(soft, drawPos, null, gold * (0.55f * glow), 0f,
                soft.Size() / 2f, 1.1f * shrink, SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, (Color.White with { A = 0 }) * (0.75f * glow),
                Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, 0.2f * shrink, SpriteEffects.None, 0f);
        }
    }
}
