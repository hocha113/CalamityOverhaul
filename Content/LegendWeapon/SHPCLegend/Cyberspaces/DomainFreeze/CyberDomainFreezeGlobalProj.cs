using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>冻结弹幕 PreDraw 着色器+PreAI 钉位</summary>
    internal class CyberDomainFreezeGlobalProj : GlobalProjectile
    {
        private static bool _shaderActive;

        public override bool PreDraw(Projectile proj, ref Color lightColor) {
            if (!CyberDomainFreeze.IsProjectileFrozen(proj.whoAmI)) return true;

            Effect shader = CyberDomainFreezeAssets.CyberFreezeEntity;
            if (shader == null) return true;

            float progress = CyberDomainFreeze.GetProjectileFreezeProgress(proj.whoAmI);
            if (progress < 0f) return true;

            float seed = 0f;
            for (int i = 0; i < CyberDomainFreeze.FrozenProjectiles.Count; i++) {
                if (CyberDomainFreeze.FrozenProjectiles[i].EntityIndex == proj.whoAmI) {
                    seed = CyberDomainFreeze.FrozenProjectiles[i].Seed;
                    break;
                }
            }

            //texelSize
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[proj.type].Value;

            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(Cyberspace.Intensity);
            shader.Parameters["seed"]?.SetValue(seed);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            return true;
        }

        public override void PostDraw(Projectile proj, Color lightColor) {
            bool wasFrozen = _shaderActive;
            if (_shaderActive) {
                _shaderActive = false;

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
            }

            //六角能量罩
            if (wasFrozen) {
                float progress = CyberDomainFreeze.GetProjectileFreezeProgress(proj.whoAmI);
                float seed = 0f;
                for (int i = 0; i < CyberDomainFreeze.FrozenProjectiles.Count; i++) {
                    if (CyberDomainFreeze.FrozenProjectiles[i].EntityIndex == proj.whoAmI) {
                        seed = CyberDomainFreeze.FrozenProjectiles[i].Seed;
                        break;
                    }
                }
                CyberDomainFreezeGlobalNPC.DrawCageOverlay(Main.spriteBatch, proj.Center,
                    Main.screenPosition, progress, seed, proj.width, proj.height);
            }
        }

        public override bool PreAI(Projectile proj) {
            if (!CyberDomainFreeze.IsProjectileFrozen(proj.whoAmI)) return true;

            //冻结位快照
            for (int i = 0; i < CyberDomainFreeze.FrozenProjectiles.Count; i++) {
                if (CyberDomainFreeze.FrozenProjectiles[i].EntityIndex == proj.whoAmI) {
                    proj.Center = CyberDomainFreeze.FrozenProjectiles[i].FreezePosition;
                    break;
                }
            }

            //冻结钉位
            proj.velocity = Vector2.Zero;
            proj.timeLeft++;
            return false;
        }
    }
}
