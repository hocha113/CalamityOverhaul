using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>放逐 NPC 绘制，PreDraw Immediate+故障 shader，PostDraw 恢复</summary>
    internal class CyberBanishNPCDraw : GlobalNPC
    {
        private static bool _shaderActive;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!CyberBanish.IsBanishing(npc.whoAmI)) return true;

            Effect shader = CyberBanishAssets.CyberBanishNPC;
            if (shader == null) return true;

            float progress = CyberBanish.GetProgress(npc.whoAmI);
            if (progress < 0f) return true;

            //texelSize
            Texture2D tex = TextureAssets.Npc[npc.type].Value;

            //entry seed
            float seed = 0f;
            for (int i = 0; i < CyberBanish.ActiveBanishments.Count; i++) {
                if (CyberBanish.ActiveBanishments[i].NpcIndex == npc.whoAmI) {
                    seed = CyberBanish.ActiveBanishments[i].Seed;
                    break;
                }
            }

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
            if (!_shaderActive) return;
            _shaderActive = false;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
