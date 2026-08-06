using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>冻结弹幕 PreDraw 着色器</summary>
    internal class CyberDomainFreezeGlobalProj : GlobalProjectile
    {
        private static bool _shaderActive;
        //记住是谁开的批次，PreDraw 链后段返回 false 时 PostDraw 不会被调用
        private static int _shaderProjIndex = -1;

        public override bool PreDraw(Projectile proj, ref Color lightColor) {
            RestoreLeakedBatch();

            Effect shader = CyberDomainFreezeAssets.CyberFreezeEntity;
            if (shader == null) return true;

            if (!CyberDomainFreeze.TryGetProjectileVisual(proj.whoAmI,
                out float progress, out float seed, out int ownerWho)) {
                return true;
            }

            //texelSize
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[proj.type].Value;

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(Cyberspace.EffectIntensityOf(ownerWho));
            shader.Parameters["seed"]?.SetValue(seed);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            _shaderProjIndex = proj.whoAmI;
            return true;
        }

        public override void PostDraw(Projectile proj, Color lightColor) {
            bool wasFrozen = _shaderActive && _shaderProjIndex == proj.whoAmI;
            EndShaderBatch();

            //六角能量罩
            if (wasFrozen && CyberDomainFreeze.TryGetProjectileVisual(proj.whoAmI,
                out float progress, out float seed, out _)) {
                CyberDomainFreezeGlobalNPC.DrawCageOverlay(Main.spriteBatch, proj.Center,
                    Main.screenPosition, progress, seed, proj.width, proj.height);
            }
        }

        private static void RestoreLeakedBatch() {
            if (_shaderActive) {
                EndShaderBatch();
            }
        }

        private static void EndShaderBatch() {
            if (!_shaderActive) {
                return;
            }
            _shaderActive = false;
            _shaderProjIndex = -1;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
