using CalamityMod;
using CalamityMod.Graphics.Renderers.CalamityRenderers;
using CalamityMod.NPCs.Providence;
using CalamityMod.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using Terraria.Graphics.Effects;
using System.Collections.Generic;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons;
using ReLogic.Content;

namespace CalamityOverhaul.Common.Effects
{
    public delegate void On_Draw_Dalegate(object inds, SpriteBatch spriteBatch);
    internal class EffectsSystem : ModSystem
    {
        internal static EffectsSystem Instance;

        public static Type holyInfernoRendererType;

        public static MethodBase onDrawToTargetMethod;

        internal static Type MiscShaderDataType;

        internal static FieldInfo Shader_Texture_FieldInfo_1;
        internal static FieldInfo Shader_Texture_FieldInfo_2;
        internal static FieldInfo Shader_Texture_FieldInfo_3;

        internal static RenderTarget2D screen;

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
            On_Main.UpdateDisplaySettings += On_Main_UpdateDisplaySettings;
        }

        public override void Unload() {
            Shader_Texture_FieldInfo_1 = null;
            Shader_Texture_FieldInfo_2 = null;
            Shader_Texture_FieldInfo_3 = null;
            On_FilterManager.EndCapture -= new On_FilterManager.hook_EndCapture(FilterManager_EndCapture);
            On_Main.UpdateDisplaySettings -= On_Main_UpdateDisplaySettings;
        }

        private void On_Main_UpdateDisplaySettings(On_Main.orig_UpdateDisplaySettings orig, Main self) {
            orig.Invoke(self);
            Main.QueueMainThreadAction(() => {
                if (screen is null || screen.Width != Main.screenWidth || screen.Height != Main.screenHeight) {
                    screen = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight);
                }
            });
        }

        private void FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, Terraria.Graphics.Effects.FilterManager self
            , RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor) {
            if (screen == null) {
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                screen = new RenderTarget2D(gd, Main.screenWidth, Main.screenHeight);
            }

            if (HasWarpEffect(out List<IDrawWarp> warpSets)) {
                GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
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

            orig.Invoke(self, finalTexture, screenTarget1, screenTarget2, clearColor);
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
