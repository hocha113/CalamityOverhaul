using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Abilities.GhostRains;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Overlay
{
    //世界内启用专属天空（GhostRainSceneEffect 在世界内让位）
    internal class OniRainWorldSkySceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && OniRainWorldState.LocalIn;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(OniRainWorldSky.Name, isActive);
    }

    /// <summary>
    /// 鬼雨世界专属天空：压顶沉云/雨幡/溺月/远山/地平积水，强度吃
    /// <see cref="GhostRainAmbience.Intensity"/>（结算后自动涨满，白闪掩护下浮现）。<br/>
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// 远雷由 <see cref="OniRainWorldState"/> 经 <see cref="NotifyThunder"/> 先闪光后延迟播声。
    /// </summary>
    internal class OniRainWorldSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OniRainWorldSky";

        private bool active;
        //天空在场 0~1，跟随氛围强度渐入渐出

        private float presence;

        //雷闪包络与二击回响，纯本地演出量
        private static float flashStrength;
        private static float flashSeed;
        private static int flashEchoTimer;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            //冷灰青微滤镜垫底（低画质回退时的主要氛围来源），透明度由 Update 驱动

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.04f, 0.05f, 0.07f)
                .UseOpacity(0f), EffectPriority.High);
        }

        /// <summary>远雷起闪：云底先亮，雷声由调用方延迟补上（光先于声）</summary>
        internal static void NotifyThunder() {
            flashStrength = 1f;
            flashSeed = Main.rand.NextFloat();
            //真实闪电常两击，隔几帧补一记回响

            flashEchoTimer = Main.rand.Next(9, 16);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || presence > 0.004f;
        public override void Reset() {
            active = false;
            presence = 0f;
            flashStrength = 0f;
            flashEchoTimer = 0;
        }

        public override void Update(GameTime gameTime) {
            //结算后 Intensity 涨满，天空随之在白闪掩护下浮现；退出世界随渐出尾巴排空
            float target = OniRainWorldState.LocalIn ? GhostRainAmbience.Intensity : 0f;
            presence = MathHelper.Lerp(presence, target, 0.10f);
            if (!active && presence < 0.004f) {
                presence = 0f;
            }

            flashStrength *= 0.86f;
            if (flashEchoTimer > 0 && --flashEchoTimer == 0) {
                flashStrength = MathHelper.Max(flashStrength, 0.55f);
            }

            //滤镜随嵌套深度加深
            Filters.Scene[Name]?.GetShader()?.UseOpacity(
                (0.20f + 0.10f * OniRainWorldState.DepthGrade) * presence);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，盖住原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (presence <= 0.004f) {
                return;
            }
            //相机捕捉路径另有一套屏幕参数篡改，世界天空不入捕捉图
            if (CaptureManager.Instance.IsCapturing) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }
            Effect shader = EffectLoader.OniRainSky?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (shader != null && noise != null) {
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                //DrawBG 窗口内 screenPosition 被加了缩放平移，还原真实相机值视差才不随缩放跳
                float camX = (Main.screenPosition - Main.BackgroundViewMatrix.Translation).X;

                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
                shader.Parameters["uIntensity"]?.SetValue(presence);
                shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                shader.Parameters["uCamX"]?.SetValue(camX);
                shader.Parameters["uFlash"]?.SetValue(flashStrength);
                shader.Parameters["uFlashSeed"]?.SetValue(flashSeed);
                shader.Parameters["uDepth"]?.SetValue(OniRainWorldState.DepthGrade);
                shader.CurrentTechnique = shader.Techniques["TechSky"];
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //着色器缺失：单层冷灰青罩底，浓度压低避免死黑块，深层略沉
                Color tint = new Color(14, 18, 22)
                    * (presence * (0.55f + 0.18f * OniRainWorldState.DepthGrade));
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), tint);
            }

            spriteBatch.End();
            //还原批次复刻 vanilla DrawBG 的精确矩阵，少了平移修正项会在缩放≠1 时偏移后续背景层

            Matrix restore = Main.BackgroundViewMatrix.TransformationMatrix;
            restore.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                * new Vector3(1f, Main.BackgroundViewMatrix.Effects
                    .HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, restore);
        }
    }
}
