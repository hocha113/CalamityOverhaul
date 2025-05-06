﻿using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    public class EffectLoader : ICWRLoader
    {
        internal static EffectLoader Instance;

        public static ArmorShaderData StreamerDustShader;
        public static ArmorShaderData InShootGlowShader;
        internal static RenderTarget2D screen;
        internal static float twistStrength = 0f;

        private static Asset<Effect> getEffect(string key) => CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffect + key, AssetRequestMode.AsyncLoad);
        private static void loadFiltersEffect(string filtersKey, string filename, string passname) {
            Asset<Effect> asset = getEffect(filename);
            Filters.Scene[filtersKey] = new Filter(new ScreenShaderData(asset, passname), EffectPriority.VeryHigh);
        }

        public static void LoadRegularShaders() {
            //Effect实例的获取被修改，它不再需要存储一个外置的字段值，因为这实际上毫无作用，使用CWRUTils.GetEffectValue()来获取这些实例
            loadFiltersEffect("CWRMod:powerSFShader", "PowerSFShader", "PowerSFShaderPass");
            loadFiltersEffect("CWRMod:warpShader", "WarpShader", "PrimitivesPass");
            loadFiltersEffect("CWRMod:neutronRingShader", "NeutronRingShader", "NeutronRingPass");
            loadFiltersEffect("CWRMod:primeHaloShader", "PrimeHaloShader", "PrimeHaloPass");
            loadFiltersEffect("CWRMod:twistColoringShader", "TwistColoring", "TwistColoringPass");
            loadFiltersEffect("CWRMod:knifeRendering", "KnifeRendering", "KnifeRenderingPass");
            loadFiltersEffect("CWRMod:knifeDistortion", "KnifeDistortion", "KnifeDistortionPass");
            loadFiltersEffect("CWRMod:gradientTrail", "GradientTrail", "GradientTrailPass");
            loadFiltersEffect("CWRMod:trailWarp", "TrailWarp", "TrailWarpPass");
            loadFiltersEffect("CWRMod:deductDraw", "DeductDraw", "DeductDrawPass");
            StreamerDustShader = new ArmorShaderData(getEffect("StreamerDust"), "StreamerDustPass");
            InShootGlowShader = new ArmorShaderData(getEffect("InShootGlow"), "InShootGlowPass");
        }

        void ICWRLoader.LoadAsset() => LoadRegularShaders();

        void ICWRLoader.LoadData() {
            Instance = this;
            On_FilterManager.EndCapture += FilterManager_EndCapture;
            Main.OnResolutionChanged += Main_OnResolutionChanged;
            On_Main.DrawDust += EndDraw;
        }

        void ICWRLoader.UnLoadData() {
            StreamerDustShader = null;
            InShootGlowShader = null;

            screen = null;

            On_FilterManager.EndCapture -= FilterManager_EndCapture;
            Main.OnResolutionChanged -= Main_OnResolutionChanged;
            On_Main.DrawDust -= EndDraw;

            Instance = null;
        }

        private void Main_OnResolutionChanged(Vector2 obj) {
            DisposeScreen();
            screen = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight);
        }

        // 确保旧的RenderTarget2D对象被正确释放
        private static void DisposeScreen() {
            screen?.Dispose();
            screen = null;
        }

        private void FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, FilterManager self,
            RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor) {

            GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;

            screen ??= new RenderTarget2D(graphicsDevice, Main.screenWidth, Main.screenHeight);

            if (!Main.gameMenu) {
                DrawPrimitiveProjectile();

                if (HasWarpEffect(out List<IWarpDrawable> warpSets, out List<IWarpDrawable> warpSetsNoBlueshift)) {
                    ProcessWarpSets(graphicsDevice, warpSets, false);
                    ProcessWarpSets(graphicsDevice, warpSetsNoBlueshift, true);
                }

                if (HasPwoerEffect()) {
                    DrawPwoerEffect(graphicsDevice, Main.spriteBatch);
                }
            }

            orig.Invoke(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }

        private void EndDraw(On_Main.orig_DrawDust orig, Main self) {
            orig(self);

            if (Main.gameMenu) {
                return;
            }

            DrawPrimitiveProjectile();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            DrawAdditiveProjectile();

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

        private static void ProcessWarpSets(GraphicsDevice graphicsDevice, List<IWarpDrawable> warpSets, bool noBlueshift) {
            if (warpSets.Count <= 0) {
                return;
            }

            // 绘制屏幕到临时目标
            graphicsDevice.SetRenderTarget(screen);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            // 绘制需要绘制的内容
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (IWarpDrawable p in warpSets) {
                p.Warp();
            }
            Main.spriteBatch.End();

            // 应用扭曲效果
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            Effect effect = CWRUtils.GetEffectValue("WarpShader");
            effect.Parameters["tex0"].SetValue(Main.screenTargetSwap);
            effect.Parameters["noBlueshift"].SetValue(noBlueshift);
            effect.Parameters["i"].SetValue(0.02f);
            effect.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(screen, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            // 绘制自定义内容
            Main.spriteBatch.Begin(default, BlendState.AlphaBlend, Main.DefaultSamplerState, default, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (IWarpDrawable p in warpSets) {
                if (p.CanDrawCustom()) {
                    p.DrawCustom(Main.spriteBatch);
                }
            }
            Main.spriteBatch.End();
        }

        private static void DrawPwoerEffect(GraphicsDevice graphicsDevice, SpriteBatch sb) {
            // 设置初始渲染目标和清除
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            // 设置绘制目标和渲染模式
            graphicsDevice.SetRenderTarget(screen);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.Transform);

            // 缓存项目符号类型和纹理以减少多次调用
            int targetProjType = ModContent.ProjectileType<MuraExecutionCutOnSpan>();
            Texture2D texture = CWRAsset.Placeholder_White.Value;
            float screenDiagonalLength = (float)Math.Sqrt(Main.screenWidth * Main.screenWidth + Main.screenHeight * Main.screenHeight) * 4f;

            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != targetProjType) {
                    continue;
                }

                Vector2 offsetRotV = proj.rotation.ToRotationVector2() * 1500;
                Vector2 basePosition = proj.Center + Vector2.Normalize((proj.Left - proj.Center)
                    .RotatedBy(proj.rotation)) * screenDiagonalLength / 2 - Main.screenPosition + offsetRotV;
                float correctedRotation = CWRUtils.GetCorrectRadian(proj.rotation);

                // 主体绘制，减少重复绘制代码
                sb.Draw(texture, basePosition, new Rectangle(0, 0, 1, 1),
                    new Color(correctedRotation, proj.ai[0], 0f, 0.2f),
                    proj.rotation, Vector2.Zero, screenDiagonalLength, SpriteEffects.None, 0);

                sb.Draw(texture, basePosition, new Rectangle(0, 0, 1, 1),
                    new Color(correctedRotation + Math.Sign(proj.rotation + 0.001f) * 0.5f, proj.ai[0], 0f, 0.2f),
                    proj.rotation, new Vector2(0, 1), screenDiagonalLength, SpriteEffects.None, 0);

                twistStrength = 0.055f * proj.localAI[0];
            }

            Main.spriteBatch.End();

            // 处理 Shader 效果
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            Effect powerSFShader = CWRUtils.GetEffectValue("PowerSFShader");
            powerSFShader.Parameters["tex0"].SetValue(screen);
            powerSFShader.Parameters["i"].SetValue(twistStrength);
            powerSFShader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }

        private static bool HasPwoerEffect() {
            if (!CWRServerConfig.Instance.MurasamaSpaceFragmentationBool) {
                return false;
            }
            return Main.LocalPlayer.CWR().EndSkillEffectStartBool;
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
