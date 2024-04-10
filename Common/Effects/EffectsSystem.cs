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
