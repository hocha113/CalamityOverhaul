using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>放逐 NPC 绘制，PreDraw Immediate+故障 shader，PostDraw 恢复</summary>
    internal class CyberBanishNPCDraw : GlobalNPC
    {
        private static bool _shaderActive;
        private static bool _scaleModified;
        private static int _scaledNpcIndex = -1;
        private static float _originalScale;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!CyberBanish.TryGetEntry(npc.whoAmI, out BanishEntry entry)) return true;

            float progress = entry.Progress;
            if (!entry.IsBoss && progress > 0.5f) {
                float shrinkPhase = (progress - 0.5f) / 0.5f;
                float shrink = 1f - MathF.Pow(shrinkPhase, 2.2f);
                _originalScale = npc.scale;
                _scaledNpcIndex = npc.whoAmI;
                _scaleModified = true;
                npc.scale = _originalScale * Math.Max(shrink, 0.02f);
            }

            Effect shader = CyberBanishAssets.CyberBanishNPC;
            if (shader == null) return true;

            //texelSize
            Texture2D tex = TextureAssets.Npc[npc.type].Value;

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(Cyberspace.Intensity);
            shader.Parameters["seed"]?.SetValue(entry.Seed);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (_scaleModified && _scaledNpcIndex == npc.whoAmI) {
                npc.scale = _originalScale;
                _scaleModified = false;
                _scaledNpcIndex = -1;
            }
            if (!_shaderActive) return;
            _shaderActive = false;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
