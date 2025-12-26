using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Common
{
    [VaultLoaden(CWRConstant.Effects)]
    public class EffectLoader : RenderHandle
    {
        public static ArmorShaderData StreamerDust { get; set; }
        public static Asset<Effect> PowerSFShader { get; set; }
        public static Asset<Effect> WarpShader { get; set; }
        public static Asset<Effect> NeutronRing { get; set; }
        public static Asset<Effect> PrimeHalo { get; set; }
        public static Asset<Effect> TwistColoring { get; set; }
        public static Asset<Effect> KnifeRendering { get; set; }
        public static Asset<Effect> KnifeDistortion { get; set; }
        public static Asset<Effect> GradientTrail { get; set; }
        public static Asset<Effect> DeductDraw { get; set; }
        public static Asset<Effect> Crystal { get; set; }
        public static Asset<Effect> AccretionDisk { get; set; }
        public static Asset<Effect> FlattenedDisk { get; set; }
        public static Asset<Effect> GammaRayBeam { get; set; }
        public override float Weight => 1.2f;
        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            DrawPrimitiveProjectile();

            if (HasWarpEffect(out List<IWarpDrawable> warpSets, out List<IWarpDrawable> warpSetsNoBlueshift)) {
                ProcessWarpSets(graphicsDevice, screenSwap, warpSets, false);
                ProcessWarpSets(graphicsDevice, screenSwap, warpSetsNoBlueshift, true);
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            DrawPrimitiveProjectile();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            DrawAdditiveProjectile();

            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (Player player in Main.ActivePlayers) {
                if (player.active && !player.DeadOrGhost &&
                    player.TryGetModPlayer(out ResurrectionDeath deathSystem)) {
                    deathSystem.DrawDeathEffects(Main.spriteBatch);
                }
            }

            Main.spriteBatch.End();
        }

        private static void DrawPrimitiveProjectile() {
            foreach (var p in Main.projectile) {
                if (p.ModProjectile == null || !p.active) {
                    continue;
                }
                if (p.ModProjectile is IPrimitiveDrawable primitive) {
                    primitive.DrawPrimitives();
                }
            }
        }

        private static void DrawAdditiveProjectile() {
            foreach (var p in Main.projectile) {
                if (p.ModProjectile == null || !p.active) {
                    continue;
                }
                if (p.ModProjectile is IAdditiveDrawable additive) {
                    additive.DrawAdditiveAfterNon(Main.spriteBatch);
                }
            }
        }

        private static void ProcessWarpSets(GraphicsDevice graphicsDevice, RenderTarget2D screen, List<IWarpDrawable> warpSets, bool noBlueshift) {
            if (warpSets.Count <= 0) {
                return;
            }

            //绘制屏幕到临时目标
            graphicsDevice.SetRenderTarget(screen);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            //绘制需要绘制的内容
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (IWarpDrawable p in warpSets) {
                p.Warp();
            }
            Main.spriteBatch.End();

            //应用扭曲效果
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            Effect effect = WarpShader.Value;
            effect.Parameters["tex0"].SetValue(Main.screenTargetSwap);
            effect.Parameters["noBlueshift"].SetValue(noBlueshift);
            effect.Parameters["i"].SetValue(0.02f);
            effect.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(screen, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            //绘制自定义内容
            Main.spriteBatch.Begin(default, BlendState.AlphaBlend, Main.DefaultSamplerState, default, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (IWarpDrawable p in warpSets) {
                if (p.CanDrawCustom()) {
                    p.DrawCustom(Main.spriteBatch);
                }
            }
            Main.spriteBatch.End();
        }

        private static bool HasWarpEffect(out List<IWarpDrawable> warpSets, out List<IWarpDrawable> warpSetsNoBlueshift) {
            warpSets = [];
            warpSetsNoBlueshift = [];

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.ModProjectile is null) {
                    continue;
                }
                if (p.ModProjectile is IWarpDrawable drawWarp) {
                    if (drawWarp.DontUseBlueshiftEffect()) {
                        warpSetsNoBlueshift.Add(drawWarp);
                    }
                    else {
                        warpSets.Add(drawWarp);
                    }
                }
            }
            return warpSets.Count > 0 || warpSetsNoBlueshift.Count > 0;
        }
    }
}
