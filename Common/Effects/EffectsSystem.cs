using CalamityMod;
using CalamityMod.Graphics.Renderers.CalamityRenderers;
using CalamityMod.NPCs;
using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Effects
{
    internal class EffectsSystem : ModSystem
    {
        public delegate void On_Draw_Dalegate(object inds, SpriteBatch spriteBatch);

        internal static EffectsSystem Instance;

        public static Type holyInfernoRendererType;

        public static MethodBase onDrawToTargetMethod;

        internal static Type MiscShaderDataType;

        internal static FieldInfo Shader_Texture_FieldInfo_1;
        internal static FieldInfo Shader_Texture_FieldInfo_2;
        internal static FieldInfo Shader_Texture_FieldInfo_3;

        internal static RenderTarget2D screen;
        internal static float twistStrength = 0f;

        public override void Load() {
            Instance = this;
            holyInfernoRendererType = typeof(HolyInfernoRenderer);
            onDrawToTargetMethod = holyInfernoRendererType.GetMethod("DrawToTarget", BindingFlags.Instance | BindingFlags.Public);
            if (onDrawToTargetMethod != null) {
                MonoModHooks.Add(onDrawToTargetMethod, OnDrawToTargetHook);
            }

            MiscShaderDataType = typeof(MiscShaderData);
            FieldInfo miscShaderGetFieldInfo(string key) => MiscShaderDataType.GetField(key, BindingFlags.NonPublic | BindingFlags.Instance);
            Shader_Texture_FieldInfo_1 = miscShaderGetFieldInfo("_uImage1");
            Shader_Texture_FieldInfo_2 = miscShaderGetFieldInfo("_uImage2");
            Shader_Texture_FieldInfo_3 = miscShaderGetFieldInfo("_uImage3");

            On_FilterManager.EndCapture += new On_FilterManager.hook_EndCapture(FilterManager_EndCapture);
            Main.OnResolutionChanged += Main_OnResolutionChanged;
        }

        public override void Unload() {
            Shader_Texture_FieldInfo_1 = null;
            Shader_Texture_FieldInfo_2 = null;
            Shader_Texture_FieldInfo_3 = null;
            On_FilterManager.EndCapture -= new On_FilterManager.hook_EndCapture(FilterManager_EndCapture);
            Main.OnResolutionChanged -= Main_OnResolutionChanged;
        }

        private void Main_OnResolutionChanged(Vector2 obj) {
            screen?.Dispose();
            screen = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight);
        }

        private void FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, Terraria.Graphics.Effects.FilterManager self
            , RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor) {
            GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
            if (screen == null) {
                screen = new RenderTarget2D(graphicsDevice, Main.screenWidth, Main.screenHeight);
            }

            if (HasWarpEffect(out List<IDrawWarp> warpSets)) {
                //绘制屏幕
                graphicsDevice.SetRenderTarget(screen);
                graphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(0, BlendState.AlphaBlend);
                Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
                //绘制需要绘制的内容
                graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
                graphicsDevice.Clear(Color.Transparent);

                Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                    , RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                foreach (IDrawWarp p in warpSets) { p.Warp(); }
                Main.spriteBatch.End();

                //应用扭曲
                graphicsDevice.SetRenderTarget(Main.screenTarget);
                graphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                //如果想热加载，最好这样获取值
                Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "WarpShader").Value;//EffectsRegistry.WarpShader;
                effect.Parameters["tex0"].SetValue(Main.screenTargetSwap);
                effect.Parameters["i"].SetValue(0.02f);
                effect.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(screen, Vector2.Zero, Color.White);
                Main.spriteBatch.End();

                Main.spriteBatch.Begin();
                foreach (IDrawWarp p in warpSets) { if (p.canDraw()) { p.costomDraw(Main.spriteBatch); } }
                Main.spriteBatch.End();
            }

            if (HasPwoerEffect()) {
                graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
                graphicsDevice.Clear(Color.Transparent);//用透明清除
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
                graphicsDevice.SetRenderTarget(screen);
                graphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.Transform);
                DrawPwoerEffect(Main.spriteBatch);
                Main.spriteBatch.End();
                graphicsDevice.SetRenderTarget(Main.screenTarget);
                graphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                EffectsRegistry.PowerSFShader.Parameters["tex0"].SetValue(screen);
                EffectsRegistry.PowerSFShader.Parameters["i"].SetValue(twistStrength);
                EffectsRegistry.PowerSFShader.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }

            orig.Invoke(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }

        private void DrawPwoerEffect(SpriteBatch sb) {
            int targetProjType = ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>();
            foreach (Projectile proj in Main.projectile) {
                Vector2 offsetRotV = proj.rotation.ToRotationVector2() * 1500;
                if (proj.type == targetProjType && proj.active) {
                    Texture2D texture = CWRUtils.GetT2DValue(CWRConstant.Placeholder2);
                    int length = (int)(Math.Sqrt(Main.screenWidth * Main.screenWidth + Main.screenHeight * Main.screenHeight) * 4f);
                    sb.Draw(texture,
                        proj.Center + Vector2.Normalize((proj.Left - proj.Center).RotatedBy(proj.rotation)) * length / 2 - Main.screenPosition + offsetRotV,
                        new(0, 0, 1, 1),
                        new(CWRUtils.GetCorrectRadian(proj.rotation), proj.ai[0], 0f, 0.2f),
                        proj.rotation,
                        Vector2.Zero,
                        length, SpriteEffects.None, 0);
                    sb.Draw(texture,
                        proj.Center + Vector2.Normalize((proj.Left - proj.Center).RotatedBy(proj.rotation)) * length / 2 - Main.screenPosition + offsetRotV,
                        new(0, 0, 1, 1),
                        new(CWRUtils.GetCorrectRadian(proj.rotation) + Math.Sign(proj.rotation + 0.001f) * 0.5f, proj.ai[0], 0f, 0.2f),
                        proj.rotation,
                        new(0, 1),
                        length,
                        SpriteEffects.None, 0);
                    twistStrength = 0.055f * proj.localAI[0];
                }
            }
        }

        private bool HasPwoerEffect() {
            if (!CWRServerConfig.Instance.MurasamaSpaceFragmentationBool) {//这里，如果配置文件关闭了碎屏效果，那么就不执行这里的特效渲染绘制
                return false;
            }
            if (!Main.LocalPlayer.CWR().EndSkillEffectStartBool) {
                return false;
            }

            return true;
        }

        private bool HasWarpEffect(out List<IDrawWarp> warpSets) {
            warpSets = new List<IDrawWarp>();
            foreach (Projectile p in Main.projectile) {
                if (!p.active) {
                    continue;
                }
                if (p.ModProjectile is IDrawWarp drawWarp) {
                    warpSets.Add(drawWarp);
                }
            }
            if (warpSets.Count > 0) {
                return true;
            }

            return false;
        }

        public static Providence Provi => Main.npc[CalamityGlobalNPC.holyBoss].ModNPC as Providence;

        public void OnDrawToTargetHook(On_Draw_Dalegate orig, object inds, SpriteBatch spriteBatch) {
            var npc = Main.npc[CalamityGlobalNPC.holyBoss];
            var borderDistance = Providence.borderRadius;
            if (!npc.HasValidTarget)
                return;

            var target = Main.player[Main.myPlayer];
            var holyInfernoIntensity = target.Calamity().holyInfernoFadeIntensity;
            var prov = Provi;

            if (prov != null) {
                var blackTile = TextureAssets.MagicPixel;
                var diagonalNoise = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/HarshNoise");
                var upwardPerlinNoise = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/Perlin");
                var upwardNoise = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/MeltyNoise");

                var maxOpacity = 1f;
                if (prov.Dying) {
                    maxOpacity = MathHelper.Lerp(1f, 0f, Utils.GetLerpValue(0f, 344f, prov.DeathAnimationTimer));
                }

                var shader = GameShaders.Misc["CalamityMod:HolyInfernoShader"].Shader;
                shader.Parameters["colorMult"].SetValue(Main.dayTime ? 7.35f : 7.65f);
                shader.Parameters["time"].SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["radius"].SetValue(borderDistance);
                shader.Parameters["anchorPoint"].SetValue(npc.Center);
                shader.Parameters["screenPosition"].SetValue(Main.screenPosition);
                shader.Parameters["screenSize"].SetValue(Main.ScreenSize.ToVector2());
                shader.Parameters["burnIntensity"].SetValue(holyInfernoIntensity);
                shader.Parameters["playerPosition"].SetValue(target.Center);
                shader.Parameters["maxOpacity"].SetValue(maxOpacity);
                shader.Parameters["day"].SetValue(Main.dayTime);

                spriteBatch.GraphicsDevice.Textures[1] = diagonalNoise.Value;
                spriteBatch.GraphicsDevice.Textures[2] = upwardNoise.Value;
                spriteBatch.GraphicsDevice.Textures[3] = upwardPerlinNoise.Value;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
                Rectangle rekt = new(Main.screenWidth / 2, Main.screenHeight / 2, Main.screenWidth, Main.screenHeight);
                spriteBatch.Draw(blackTile.Value, rekt, null, default, 0f, blackTile.Value.Size() * 0.5f, 0, 0f);
                spriteBatch.ExitShaderRegion();
            }
        }
    }
}
