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

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!ShouldApplyEffect(npc)) return true;

            Effect shader = CyberDomainFreezeAssets.CyberFreezeEntity;
            if (shader == null) return true;

            float progress = CyberDomainFreeze.GetNPCFreezeProgress(npc.whoAmI);
            if (progress < 0f) return true;

            Texture2D tex = TextureAssets.Npc[npc.type].Value;

            float seed = CyberDomainFreeze.GetNPCSeed(npc.whoAmI);

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(Cyberspace.Intensity);
            shader.Parameters["seed"]?.SetValue(seed);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            bool wasFrozen = _shaderActive;
            if (_shaderActive) {
                _shaderActive = false;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
            }

            //绘制六角能量罩覆盖层
            if (wasFrozen) {
                float progress = CyberDomainFreeze.GetNPCFreezeProgress(npc.whoAmI);
                float seed = CyberDomainFreeze.GetNPCSeed(npc.whoAmI);
                DrawCageOverlay(spriteBatch, npc.Center, screenPos, progress, seed, npc.width, npc.height);
            }
        }

        /// <summary>冻结实体六角能量罩覆盖</summary>
        internal static void DrawCageOverlay(SpriteBatch spriteBatch, Vector2 worldCenter, Vector2 screenPos,
            float progress, float seed, int entityWidth, int entityHeight) {
            Effect cageShader = CyberDomainFreezeAssets.CyberFreezeCage;
            if (cageShader == null || progress < 0f) return;

            float cageRadius = Math.Max(entityWidth, entityHeight) * 0.5f + 20f;
            float quadSize = cageRadius * 2.4f;

            //形成进度: 前30帧 (0.5秒) 从0到1
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

        public static bool? PreAIByOverNPC(NPC npc) {
            if (!CyberDomainFreeze.IsNPCFrozen(npc.whoAmI)) return null;

            //获取冻结位置快照
            for (int i = 0; i < CyberDomainFreeze.FrozenNPCs.Count; i++) {
                if (CyberDomainFreeze.FrozenNPCs[i].EntityIndex == npc.whoAmI) {
                    npc.Center = CyberDomainFreeze.FrozenNPCs[i].FreezePosition;
                    break;
                }
            }

            npc.velocity = Vector2.Zero;
            npc.frameCounter = 0;
            npc.timeLeft++;
            return false;
        }

        public override bool PreAI(NPC npc) {
            //PreAIByOverNPC 做停滞
            return true;
        }

        private static bool ShouldApplyEffect(NPC npc) {
            if (!CyberDomainFreeze.IsNPCFrozen(npc.whoAmI)) return false;
            //放逐中 NPC 走放逐 shader，不叠冻结
            if (CyberBanish.IsBanishing(npc.whoAmI)) return false;
            return true;
        }
    }
}
