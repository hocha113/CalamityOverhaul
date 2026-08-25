using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>教徒绘制装配：符印 quad、真身/假身本体分层</summary>
    internal static class CultistRenderHelper
    {
        /// <summary>
        /// 仪式符印 quad（CultistRuneSigil.fx）<br/>
        /// 合同同 ShockRingDraw.Draw：调用方须处于实体绘制批（Deferred AlphaBlend），
        /// 内部切 Immediate+Additive 画 quad 后还原；着色器缺失时走 DiffusionCircle 精灵回退
        /// </summary>
        /// <param name="sb">当前处于实体批的 SpriteBatch</param>
        /// <param name="worldPos">印心世界坐标</param>
        /// <param name="radiusPx">外环可见半径（世界px）</param>
        /// <param name="tint">元素染色</param>
        /// <param name="progress">0~1 弧序描绘进度</param>
        /// <param name="commit">0~1 定形迸发</param>
        /// <param name="fill">0~1 充能扇区</param>
        /// <param name="alpha">整体透明度</param>
        public static void DrawSigil(SpriteBatch sb, Vector2 worldPos, float radiusPx,
            Color tint, float progress, float commit, float fill, float alpha) {
            if (alpha <= 0.01f || progress <= 0.001f || radiusPx < 4f) {
                return;
            }

            Effect effect = EffectLoader.CultistRuneSigil?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                DrawSigilFallback(sb, worldPos, radiusPx, tint, alpha * progress);
                return;
            }

            //shader 外环位于内容半径 0.84，quad 折算后留护栏余量
            float halfPx = radiusPx / 0.84f / 0.92f;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + worldPos.X * 0.0007f);
            effect.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
            effect.Parameters["uTint"]?.SetValue(tint.ToVector3());
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uCommit"]?.SetValue(MathHelper.Clamp(commit, 0f, 1f));
            effect.Parameters["uFill"]?.SetValue(MathHelper.Clamp(fill, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            float quadSize = halfPx * 2f;
            sb.Draw(canvas, worldPos - Main.screenPosition, null, Color.White, 0f, canvas.Size() * 0.5f,
                quadSize / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>精灵回退：双扩散环近似（黑底加色安全）</summary>
        private static void DrawSigilFallback(SpriteBatch sb, Vector2 worldPos, float radiusPx, Color tint, float alpha) {
            Texture2D body = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle5")?.Value;
            Texture2D rim = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            if (body == null || rim == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 drawPos = worldPos - Main.screenPosition;
            Color tintA = tint with { A = 255 };
            float bodyScale = radiusPx / (body.Width * 0.5f * 0.39f);
            float rimScale = radiusPx / (rim.Width * 0.5f * 0.95f);
            sb.Draw(body, drawPos, null, tintA * (alpha * 0.7f), 0f, body.Size() * 0.5f, bodyScale, SpriteEffects.None, 0f);
            sb.Draw(rim, drawPos, null, tintA * (alpha * 0.55f), 0f, rim.Size() * 0.5f, rimScale, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 真身本体：背后仪式法阵（充能表）→ 施法辉光 → 真身足影（识真线索）→ vanilla 帧体
        /// </summary>
        public static void DrawBody(SpriteBatch sb, NPC npc, CultistStateContext context, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.CultistBoss);
            Texture2D tex = TextureAssets.Npc[NPCID.CultistBoss].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float bodyAlpha = 1f - npc.alpha / 255f;
            if (bodyAlpha <= 0.004f) {
                return;
            }

            Vector2 drawPos = npc.Center - screenPos;
            Color elemCore = CultistMotion.PhaseCore(context.Phase);

            //背后仪式法阵：充能表本体，全程可读
            if (context.SigilReveal > 0.01f) {
                DrawSigil(sb, npc.Center, 118f, elemCore,
                    context.SigilReveal, context.SigilCommit,
                    context.RitualCharge / CultistStateContext.RitualMax,
                    (0.5f + context.ChantGlow * 0.5f) * bodyAlpha);
            }

            //施法辉光
            if (context.CastAura > 0.01f) {
                Color aura = context.AuraColor with { A = 0 };
                sb.Draw(glow, drawPos, null, aura * (0.55f * context.CastAura * bodyAlpha), 0f,
                    glow.Size() * 0.5f, 2.6f * context.CastAura + 1.2f, SpriteEffects.None, 0f);
            }

            //真身足影：脚下光渍，假身没有，识真的静态线索
            Vector2 footPos = drawPos + new Vector2(0f, npc.height * 0.5f + 12f);
            Color shadowTint = elemCore with { A = 0 };
            sb.Draw(glow, footPos, null, shadowTint * (0.5f * bodyAlpha), 0f, glow.Size() * 0.5f,
                new Vector2(1.9f, 0.55f), SpriteEffects.None, 0f);

            //vanilla 帧体
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = npc.frame.Size() * 0.5f;
            Vector2 bodyPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY + 4f);
            sb.Draw(tex, bodyPos, npc.frame, drawColor * bodyAlpha, npc.rotation, origin,
                npc.scale * context.ScalePulse, flip, 0f);

            //咏唱炽体：同帧加色复写，白热从体内透出
            if (context.ChantGlow > 0.02f) {
                Color hot = elemCore with { A = 0 };
                sb.Draw(tex, bodyPos, npc.frame, hot * (0.55f * context.ChantGlow * bodyAlpha), npc.rotation, origin,
                    npc.scale * context.ScalePulse * (1f + context.ChantGlow * 0.03f), flip, 0f);
            }
        }

        /// <summary>
        /// 假身本体：无足影、无法阵、体色去饱和偏苍，三条识别线索的静态两条
        /// </summary>
        public static void DrawCloneBody(SpriteBatch sb, NPC npc, Vector2 screenPos, Color drawColor, float paleness) {
            Main.instance.LoadNPC(NPCID.CultistBossClone);
            Texture2D tex = TextureAssets.Npc[NPCID.CultistBossClone].Value;
            float bodyAlpha = 1f - npc.alpha / 255f;
            if (bodyAlpha <= 0.004f) {
                return;
            }

            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = npc.frame.Size() * 0.5f;
            Vector2 bodyPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY + 4f);

            //苍白化：体色向灰青拉，观感"少了一层活气"
            Color pale = Color.Lerp(drawColor, CultistMotion.PaleClone.MultiplyRGB(drawColor), paleness);
            sb.Draw(tex, bodyPos, npc.frame, pale * bodyAlpha, npc.rotation, origin, npc.scale, flip, 0f);
        }
    }
}
