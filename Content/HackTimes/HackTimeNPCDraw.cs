using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    //骇客时间 NPC 高亮着色器
    internal class HackTimeNPCDraw : GlobalNPC
    {
        private static bool _shaderActive;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!ShouldApplyEffect(npc)) return true;

            Effect shader = HackTimeAssets.HackTimeNPCHighlight;
            if (shader == null) return true;

            bool isSelected = npc.whoAmI == HackTime.SelectedTargetIndex;
            float effectStr = HackTime.Intensity;

            Texture2D tex = TextureAssets.Npc[npc.type].Value;

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["intensity"]?.SetValue(effectStr);
            shader.Parameters["isSelected"]?.SetValue(isSelected ? 1f : 0f);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

            //Immediate 套着色器
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

            //恢复 Deferred 模式
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static bool ShouldApplyEffect(NPC npc) {
            if (!HackTime.Active && HackTime.Intensity < 0.01f) return false;
            //放逐 NPC 不叠加高亮
            if (LegendWeapon.SHPCLegend.Cyberspaces.Banish.CyberBanish.IsBanishing(npc.whoAmI)) return false;
            //领域冻结 NPC 不叠加高亮
            if (LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze.CyberDomainFreeze.IsNPCFrozen(npc.whoAmI)) return false;
            return npc.whoAmI == HackTime.SelectedTargetIndex
                || npc.whoAmI == HackTime.HoveredTargetIndex;
        }
    }
}
