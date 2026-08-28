using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities.GhostRains
{
    /// <summary>
    /// 阴幕强度控制器：取本地视野内所有鬼雨控制器的包络峰值，纯本地演出量。<br/>
    /// 鬼雨世界（<see cref="Scenarios.OniRainWorlds.OniRainWorldState"/>）也从这里喂强度，复用同一套天幕/滤镜/压顶。
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
            float target = Scenarios.OniRainWorlds.OniRainWorldState.GlobalAmbientTarget;
            //强度已归零且近两帧无雨幕盖戳时跳过全表扫描；强度未归零则继续扫，
            //时停中 AI 停摆（戳过期）也能靠这条继续找到冻结的雨幕、演出不塌
            if (Intensity > 0f || GhostRainProj.PresenceStamp.ActiveWithin()) {
                int type = ModContent.ProjectileType<GhostRainProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (!projectile.active || projectile.type != type
                        || projectile.ModProjectile is not GhostRainProj rain) {
                        continue;
                    }
                    Player owner = Main.player[projectile.owner];
                    if (owner?.active != true) {
                        continue;
                    }
                    float envelope = rain.Presence;
                    //远处别人的鬼雨不压暗本地屏幕
                    float distance = Vector2.Distance(owner.Center, Main.LocalPlayer.Center);
                    float near = 1f - MathHelper.Clamp(
                        (distance - (GhostRainStorm.Radius + 300f)) / 900f, 0f, 1f);
                    target = Math.Max(target, envelope * near);
                }
            }
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

            //鬼雨世界深层的附加压顶：只随嵌套深度走，役鬼鬼雨不受影响
            float depth = Scenarios.OniRainWorlds.OniRainWorldState.DepthGrade;
            if (depth > 0.001f) {
                Color deepTile = new(30, 38, 44);
                Color deepBg = new(18, 24, 30);
                tileColor = Color.Lerp(tileColor, deepTile, depth * 0.45f);
                backgroundColor = Color.Lerp(backgroundColor, deepBg, depth * 0.5f);
            }
        }
    }

    //阴幕在场期间启用天空替换；鬼雨世界内让位给专属天空 OniRainWorldSky
    internal class GhostRainSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && GhostRainAmbience.Visible
            && !Scenarios.OniRainWorlds.OniRainWorldState.LocalIn;
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
