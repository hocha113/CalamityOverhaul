using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Backgrounds
{
    //子世界内启用专属天空；音乐置静，雨声与远雷是这里唯一的声部
    internal class KiameSkyScene : ModSceneEffect
    {
        public override int Music => 0;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => KiameWorld.Active && !Main.gameMenu;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(KiameSky.Name, isActive);
    }

    /// <summary>
    /// 鬼雨子世界天空：复用 OniRainSky 着色器（压顶沉云/雨幡/溺月/远山/地平积水），
    /// 这里雨是实景，深度档钉在深层。<br/>
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// 远雷由 <see cref="KiameAmbience"/> 经 <see cref="NotifyThunder"/> 先闪光后延迟播声。
    /// </summary>
    internal class KiameSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:KiameSky";

        //实景雨的天穹深度档：比叠加层的第一层沉，留一丝余量给演出
        private const float SkyDepthGrade = 0.85f;

        private bool active;
        //天空在场 0~1，跟随氛围包络渐入渐出
        private float presence;

        //雷闪包络与二击回响，纯本地演出量
        private static float flashStrength;
        private static float flashSeed;
        private static int flashEchoTimer;

        /// <summary>当前雷闪包络 0~1，水面回照与氛围层共读</summary>
        internal static float FlashStrength => flashStrength;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            //冷灰青微滤镜垫底（低画质回退时的主要氛围来源），透明度由 Update 驱动
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.03f, 0.045f, 0.055f)
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
            float target = KiameWorld.Active ? KiameAmbience.Presence : 0f;
            presence = MathHelper.Lerp(presence, target, 0.10f);
            if (!active && presence < 0.004f) {
                presence = 0f;
            }

            flashStrength *= 0.86f;
            if (flashEchoTimer > 0 && --flashEchoTimer == 0) {
                flashStrength = MathHelper.Max(flashStrength, 0.55f);
            }

            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.26f * presence);
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
                shader.Parameters["uDepth"]?.SetValue(SkyDepthGrade);
                shader.CurrentTechnique = shader.Techniques["TechSky"];
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //着色器缺失：单层冷灰青罩底，浓度压低避免死黑块
                Color tint = new Color(14, 18, 22) * (presence * 0.68f);
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
