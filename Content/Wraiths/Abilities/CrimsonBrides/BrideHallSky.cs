using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities.CrimsonBrides
{
    /// <summary>
    /// 冷喜小域强度控制器：取本地视野内所有迎亲仪式的包络峰值，纯本地演出量。<br/>
    /// 相位包络由仪式计时直接给出，中止跳段时用平滑收拢避免跳变。
    /// </summary>
    internal static class BrideHall
    {
        /// <summary>本地屏幕的冷喜在场强度 0~1</summary>
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
                    || !player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                    || rite.RiteTimer <= 0) {
                    continue;
                }
                float envelope = RiteEnvelope(rite.RiteTimer);
                //远处别人的喜堂不压暗本地屏幕
                float distance = Vector2.Distance(player.Center, Main.LocalPlayer.Center);
                float near = 1f - MathHelper.Clamp((distance - 1200f) / 1000f, 0f, 1f);
                target = Math.Max(target, envelope * near);
            }
            //中止跳段的强度落差走平滑，正常相位包络本身已连续
            Intensity = Math.Abs(target - Intensity) < 0.01f
                ? target : MathHelper.Lerp(Intensity, target, 0.22f);
        }

        internal static void Reset() => Intensity = 0f;

        /// <summary>轿至爬升→迎入/合卺满值→散场排干</summary>
        internal static float RiteEnvelope(int timer) {
            if (timer <= 0) {
                return 0f;
            }
            if (timer <= CrimsonBrideRestart.PhaseArriveEnd) {
                return 0.55f * timer / CrimsonBrideRestart.PhaseArriveEnd;
            }
            if (timer <= CrimsonBrideRestart.PhaseWelcomeEnd) {
                float k = (timer - CrimsonBrideRestart.PhaseArriveEnd)
                    / (float)(CrimsonBrideRestart.PhaseWelcomeEnd - CrimsonBrideRestart.PhaseArriveEnd);
                return MathHelper.Lerp(0.55f, 1f, k);
            }
            if (timer <= CrimsonBrideRestart.PhaseUnionEnd) {
                return 1f;
            }
            float depart = (timer - CrimsonBrideRestart.PhaseUnionEnd)
                / (float)(CrimsonBrideRestart.TotalFrames - CrimsonBrideRestart.PhaseUnionEnd);
            return MathHelper.Clamp(1f - depart, 0f, 1f);
        }
    }

    internal class BrideHallSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                BrideHall.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                BrideHall.Reset();
            }
        }

        //日光压向冷墨红，喜堂真正的"压暗"靠这里
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float hall = BrideHall.Intensity;
            if (hall <= 0.001f) {
                return;
            }
            Color hallTile = new(64, 22, 30);
            Color hallBg = new(44, 14, 22);
            tileColor = Color.Lerp(tileColor, hallTile, hall * 0.55f);
            backgroundColor = Color.Lerp(backgroundColor, hallBg, hall * 0.60f);
        }
    }

    //冷喜在场期间启用天空替换
    internal class BrideHallSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && BrideHall.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(BrideHallSky.Name, isActive);
    }

    /// <summary>
    /// 无人喜堂的冷墨红天幕，强度由 <see cref="BrideHall.Intensity"/> 驱动。<br/>
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// IsActive 只反映激活态，渐出尾巴由 <see cref="BrideHall.Visible"/> 兜住。
    /// </summary>
    internal class BrideHallSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:BrideHallSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.09f, 0.015f, 0.03f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.20f * BrideHall.Intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，盖住原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float presence = BrideHall.Intensity;
            if (presence <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.BrideCurtain?.Value;
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
                shader.Parameters["uIntensity"]?.SetValue(presence);
                shader.CurrentTechnique = shader.Techniques["TechHall"];
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //着色器缺失：单层冷墨红罩底，浓度压低避免死黑块
                Color tint = new Color(26, 8, 12) * (presence * 0.42f);
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), tint);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
