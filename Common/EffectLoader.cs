using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Common
{
    [VaultLoaden(CWRConstant.Effects)]
    public class EffectLoader : RenderHandle
    {
        public static Asset<Effect> PowerSFShader { get; set; }
        public static Asset<Effect> WarpShader { get; set; }
        public static Asset<Effect> NeutronRing { get; set; }
        public static Asset<Effect> NeutronWarp { get; set; }
        public static Asset<Effect> PrimeHalo { get; set; }
        public static Asset<Effect> DestroyerThermalOutline { get; set; }
        public static Asset<Effect> KnifeRendering { get; set; }
        public static Asset<Effect> KnifeDistortion { get; set; }
        public static Asset<Effect> GradientTrail { get; set; }
        public static Asset<Effect> DeductDraw { get; set; }
        public static Asset<Effect> Crystal { get; set; }
        public static Asset<Effect> AccretionDisk { get; set; }
        public static Asset<Effect> FlattenedDisk { get; set; }
        public static Asset<Effect> BlackHole { get; set; }
        public static Asset<Effect> GammaRayBeam { get; set; }
        public static Asset<Effect> DropPodFlame { get; set; }
        public static Asset<Effect> DropPodShockwave { get; set; }
        public static Asset<Effect> DropPodHeatHaze { get; set; }
        public static Asset<Effect> CyberShockwave { get; set; }
        public static Asset<Effect> CyberRestartField { get; set; }
        public static Asset<Effect> CyberBoundaryRing { get; set; }
        public static Asset<Effect> CyberGlitchBolt { get; set; }
        public static Asset<Effect> CyberRiftSlash { get; set; }
        public static Asset<Effect> CyberReform { get; set; }
        public static Asset<Effect> CyberTraceBeam { get; set; }
        public static Asset<Effect> CyberEnergyOrb { get; set; }
        public static Asset<Effect> CyberDetonation { get; set; }
        public static Asset<Effect> CyberDataArc { get; set; }
        public static Asset<Effect> CyberspaceField { get; set; }
        public static Asset<Effect> CyberPanel { get; set; }
        public static Asset<Effect> CyberDomainPanel { get; set; }
        public static Asset<Effect> SHPCModPanel { get; set; }
        public static Asset<Effect> CyberpunkItemFilter { get; set; }
        public static Asset<Effect> HotwindPanel { get; set; }
        public static Asset<Effect> DraedonPanel { get; set; }
        public static Asset<Effect> ForestPanel { get; set; }
        public static Asset<Effect> NotifBadge { get; set; }
        public static Asset<Effect> ShepelGlitch { get; set; }
        public static Asset<Effect> SeaDomainField { get; set; }
        public static Asset<Effect> OceanCurrentTrail { get; set; }
        public static Asset<Effect> OceanWaterBlob { get; set; }
        public static Asset<Effect> ElysiumHalo { get; set; }
        public static Asset<Effect> ElysiumStaff { get; set; }
        public static Asset<Effect> SerpentTrail { get; set; }
        public static Asset<Effect> CelestialStar { get; set; }
        public static Asset<Effect> BrimstoneDomain { get; set; }
        public static Asset<Effect> BrimstoneBlastWave { get; set; }
        public static Asset<Effect> KingSlimeRoyalAura { get; set; }
        public static Asset<Effect> KingSlimeShockwave { get; set; }
        public static Asset<Effect> KingSlimeRoyalBeam { get; set; }
        public static Asset<Effect> KingSlimeBloodWing { get; set; }
        public static Asset<Effect> WitchBrimstoneDomain { get; set; }
        public static Asset<Effect> CelestialDomain { get; set; }
        public static Asset<Effect> ProverbsGhostDomain { get; set; }
        public static Asset<Effect> RevelationPlague { get; set; }
        public static Asset<Effect> VoidPortal { get; set; }
        public static Asset<Effect> AbandonedPortalPanel { get; set; }
        public static Asset<Effect> VoidSuction { get; set; }
        public static Asset<Effect> VoidArrival { get; set; }
        public static Asset<Effect> CyberBossBar { get; set; }
        public static Asset<Effect> HackRamArc { get; set; }
        public static Asset<Effect> SHPCCoreOrb { get; set; }
        public static Asset<Effect> CyberwareRadialPanel { get; set; }
        public static Asset<Effect> ThermalPanel { get; set; }
        public static Asset<Effect> ThermalBar { get; set; }
        public static Asset<Effect> ThermalHeatHaze { get; set; }
        public static Asset<Effect> VoidColonySky { get; set; }
        public static Asset<Effect> VoidFog { get; set; }
        public static Asset<Effect> VoidTimeShift { get; set; }
        public static Asset<Effect> GlitchHead { get; set; }
        public static Asset<Effect> ArchitectureWarp { get; set; }
        public static Asset<Effect> VoidLaserCannon { get; set; }
        public static Asset<Effect> SignalTowerLightning { get; set; }
        public static Asset<Effect> SignalTowerElectrified { get; set; }
        public static Asset<Effect> SignalTowerVirusBroadcast { get; set; }
        public static Asset<Effect> SignalTowerHoverOutline { get; set; }
        public static Asset<Effect> DecryptionPanelBackground { get; set; }
        public static Asset<Effect> BreachMatrixAxisHighlight { get; set; }
        public static Asset<Effect> GatlinTracer { get; set; }
        public static Asset<Effect> GatlinImpactBurst { get; set; }
        public static Asset<Effect> BrimstoneDialogueBox { get; set; }
        public static Asset<Effect> SeaDialogueBox { get; set; }
        public static Asset<Effect> EntrustGuideCard { get; set; }
        public static Asset<Effect> MurasamaPhantomPanel { get; set; }
        public static Asset<Effect> CybCourseSky { get; set; }
        public static Asset<Effect> CybCourseLoading { get; set; }
        public static Asset<Effect> CybCourseEntryReveal { get; set; }
        public static Asset<Effect> VoidColonyLoading { get; set; }
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
            effect.Parameters["i"].SetValue(0.035f);
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
