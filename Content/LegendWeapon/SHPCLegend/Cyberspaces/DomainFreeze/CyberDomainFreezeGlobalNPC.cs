using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>冻结 NPC PreDraw 着色器</summary>
    internal class CyberDomainFreezeGlobalNPC : GlobalNPC
    {
        private static bool _shaderActive;
        //记住是谁开的批次：PreDraw 链后段返回 false 时 PostDraw 不会被调用，得让下一个实体自愈
        private static int _shaderNpcIndex = -1;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            RestoreLeakedBatch(spriteBatch);

            if (!ShouldApplyEffect(npc)) return true;

            Effect shader = CyberDomainFreezeAssets.CyberFreezeEntity;
            if (shader == null) return true;

            if (!CyberDomainFreeze.TryGetNPCVisual(npc.whoAmI, out float progress,
                out float seed, out int ownerWho)) {
                return true;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(Cyberspace.EffectIntensityOf(ownerWho));
            shader.Parameters["seed"]?.SetValue(seed);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            _shaderNpcIndex = npc.whoAmI;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            bool wasFrozen = _shaderActive && _shaderNpcIndex == npc.whoAmI;
            EndShaderBatch(spriteBatch);

            //六角能量罩
            if (wasFrozen && CyberDomainFreeze.TryGetNPCVisual(npc.whoAmI,
                out float progress, out float seed, out _)) {
                DrawCageOverlay(spriteBatch, npc.Center, screenPos, progress, seed, npc.width, npc.height);
            }
        }

        private static void RestoreLeakedBatch(SpriteBatch spriteBatch) {
            if (_shaderActive) {
                EndShaderBatch(spriteBatch);
            }
        }

        private static void EndShaderBatch(SpriteBatch spriteBatch) {
            if (!_shaderActive) {
                return;
            }
            _shaderActive = false;
            _shaderNpcIndex = -1;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>冻结实体六角能量罩覆盖</summary>
        internal static void DrawCageOverlay(SpriteBatch spriteBatch, Vector2 worldCenter, Vector2 screenPos,
            float progress, float seed, int entityWidth, int entityHeight) {
            Effect cageShader = CyberDomainFreezeAssets.CyberFreezeCage;
            if (cageShader == null || progress < 0f) return;

            float cageRadius = Math.Max(entityWidth, entityHeight) * 0.5f + 20f;
            float quadSize = cageRadius * 2.4f;

            //形成进度，前30帧 0→1
            float formProgress = Math.Min(progress * (CyberDomainFreeze.DefaultFreezeDuration / 30f), 1f);

            cageShader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            cageShader.Parameters["progress"]?.SetValue(progress);
            cageShader.Parameters["formProgress"]?.SetValue(formProgress);
            cageShader.Parameters["seed"]?.SetValue(seed);

            Vector2 drawPos = worldCenter - screenPos;
            Texture2D canvas = VaultAsset.placeholder2.Value;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            cageShader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(quadSize, quadSize),
                SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static bool ShouldApplyEffect(NPC npc) {
            if (!CyberDomainFreeze.IsNPCFrozen(npc.whoAmI)) return false;
            //放逐中不叠冻结 shader
            if (CyberBanish.IsBanishing(npc.whoAmI)) return false;
            return true;
        }
    }
}
