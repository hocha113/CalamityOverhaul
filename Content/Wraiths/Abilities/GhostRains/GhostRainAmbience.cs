using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities.GhostRains
{
    /// <summary>
    /// 阴幕强度控制器：取本地视野内所有鬼雨风暴的包络峰值，纯本地演出量。<br/>
    /// 相位包络由风暴计时直接给出，中止跳段时用平滑收拢避免跳变。
    /// </summary>
    internal static class GhostRainAmbience
    {
        /// <summary>本地屏幕的阴幕在场强度 0~1</summary>
        public static float Intensity { get; private set; }

        /// <summary>仍需在场（含渐出尾巴）</summary>
        public static bool Visible => Intensity > 0.004f;

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Intensity = 0f;
                return;
            }
            float target = 0f;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true
                    || !player.TryGetModPlayer(out GhostRainStormPlayer storm)
                    || storm.StormTimer <= 0) {
                    continue;
                }
                float envelope = GhostRainStorm.Envelope(storm.StormTimer);
                //远处别人的鬼雨不压暗本地屏幕
                float distance = Vector2.Distance(player.Center, Main.LocalPlayer.Center);
                float near = 1f - MathHelper.Clamp(
                    (distance - (GhostRainStorm.Radius + 300f)) / 900f, 0f, 1f);
                target = Math.Max(target, envelope * near);
            }
            //中止跳段的强度落差走平滑，正常相位包络本身已连续
            Intensity = Math.Abs(target - Intensity) < 0.01f
                ? target : MathHelper.Lerp(Intensity, target, 0.22f);
        }

        internal static void Reset() => Intensity = 0f;
    }

    internal class GhostRainAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                GhostRainAmbience.Update();
                GhostRainFx.Sweep();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                GhostRainAmbience.Reset();
                GhostRainFx.Clear();
            }
        }

        //日光勒向冷湿灰青，鬼雨真正的"压顶"靠这里
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float veil = GhostRainAmbience.Intensity;
            if (veil <= 0.001f) {
                return;
            }
            Color rainTile = new(52, 62, 68);
            Color rainBg = new(34, 42, 48);
            tileColor = Color.Lerp(tileColor, rainTile, veil * 0.55f);
            backgroundColor = Color.Lerp(backgroundColor, rainBg, veil * 0.62f);
        }
    }

    //阴幕在场期间启用天空替换
    internal class GhostRainSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && GhostRainAmbience.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(GhostRainSky.Name, isActive);
    }

    /// <summary>
    /// 湿墨阴幕天空，强度由 <see cref="GhostRainAmbience.Intensity"/> 驱动。<br/>
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// IsActive 只反映激活态，渐出尾巴由 <see cref="GhostRainAmbience.Visible"/> 兜住。
    /// </summary>
    internal class GhostRainSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:GhostRainSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.04f, 0.05f, 0.07f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.28f * GhostRainAmbience.Intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，盖住原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float presence = GhostRainAmbience.Intensity;
            if (presence <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.GhostRain?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (white == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (shader != null && noise != null) {
                shader.Parameters["uNoiseTex"]?.SetValue(noise);
                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
                shader.Parameters["uSeed"]?.SetValue(3.7f);
                shader.Parameters["uIntensity"]?.SetValue(presence);
                shader.CurrentTechnique = shader.Techniques["TechSky"];
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //着色器缺失：单层冷灰青罩底，浓度压低避免死黑块
                Color tint = new Color(16, 20, 24) * (presence * 0.40f);
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), tint);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
