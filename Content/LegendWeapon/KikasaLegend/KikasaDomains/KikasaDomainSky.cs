using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>血湖领域天空。玩家主动能力，不走 ModSceneEffect 场景竞争，由 <see cref="KikasaDomainSystem"/> 每帧手动驱动激活</summary>
    internal class KikasaDomainSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:KikasaDomainSky";

        private bool active;
        //天空在场 0~1，跟随撕开覆盖

        private float presence;

        //异化态远雷的闪光包络，纯本地演出量
        private static float flashStrength;
        private static int flashEchoTimer;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册，KikasaDomainSystem 按该名驱动，缺 Filter 直接 NRE

            SkyManager.Instance[Name] = this;
            //空气层微滤镜（瘀青靛垫底），颜色与透明度由 Update 动态驱动

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.045f, 0.058f, 0.125f)
                .UseOpacity(0f), EffectPriority.High);
        }

        /// <summary>异化态远雷起闪：云底先亮，雷声由调用方延迟补上（光先于声）</summary>
        internal static void NotifyThunder() {
            flashStrength = 1f;
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
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;

            if (kdp != null && kdp.AnyActive) {
                //空间撕纸遮罩负责揭示，presence 直接快速就位

                presence = MathHelper.Lerp(presence, 1f, 0.45f);
            }
            else {
                //域已闭、Update 排空缓存，Draw 同帧按实时状态截断

                presence = 0f;
            }

            flashStrength *= 0.86f;
            if (flashEchoTimer > 0 && --flashEchoTimer == 0) {
                flashStrength = MathHelper.Max(flashStrength, 0.55f);
            }

            //滤镜垫底色（低画质回退时的主要氛围来源）：瘀青靛↔冷灰青随异化过渡；
            //鬼梦期让位梦空滤镜

            float rain = kdp?.RainBlend ?? 0f;
            float dreamYield = 1f - (kdp?.DreamBlend ?? 0f);
            Vector3 filterCol = Vector3.Lerp(
                new Vector3(0.045f, 0.058f, 0.125f),
                new Vector3(0.04f, 0.05f, 0.07f), rain);
            Filters.Scene[Name]?.GetShader()
                ?.UseColor(filterCol.X, filterCol.Y, filterCol.Z)
                .UseOpacity((0.12f + 0.06f * rain) * presence
                    * (kdp?.PresenceSmooth ?? 0f) * dreamYield);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.AnyActive) {
                Filters.Scene[Name]?.GetShader()?.UseOpacity(0f);
                return;
            }
            //跨 0 深度切片只画一次、该切片在所有原版背景层之后绘制，覆盖山野等背景

            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (presence <= 0.004f) {
                return;
            }
            //相机捕捉路径另有一套屏幕参数篡改，域天空不入捕捉图

            if (CaptureManager.Instance.IsCapturing) {
                return;
            }
            Effect shader = EffectLoader.KikasaSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            //DrawBG 窗口内 screenWidth/Height 被除以背景缩放、screenPosition 加了缩放平移(Main.DoDraw)

            //须还原真实相机值，撕纸圆才能与 KikasaGrade 在任意缩放下逐像素重合

            Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;
            Vector2 realScreenSize = new(vpW, vpH);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            bool spread = kdp.Phase == KikasaDomainPhase.Opening || kdp.Phase == KikasaDomainPhase.Closing;
            //原点与遮罩噪声时间都必须与 KikasaGrade 同源(GameViewMatrix 未被背景窗口篡改，可直接用)

            Vector2 origin = Vector2.Transform(
                kdp.OriginWorldPos - realScreenPos,
                Main.GameViewMatrix.TransformationMatrix);

            //真水线（视口 uv）：与 KikasaGrade.SetSharedParams 同公式，
            //只是投影用还原后的真实相机值，水线以下天空换成实体湖体，垫在湖面着色器之下

            float pivotUv = MathHelper.Clamp(Vector2.Transform(
                new Vector2(realScreenPos.X, kdp.LakeWorldY) - realScreenPos,
                Main.GameViewMatrix.TransformationMatrix).Y / vpH, -8f, 8f);
            float waterLevel = MathHelper.Lerp(1.15f, pivotUv, kdp.RiseProgress);
            float waterWobble = 0.0025f + 0.011f * kdp.FoamBoost;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            //鬼梦期血暮天空让位，交叉渐变在结算闪掩护下完成
            shader.Parameters["uSkyAlpha"]?.SetValue(presence * (1f - kdp.DreamBlend));
            //遮罩空间尺寸取视口真实像素，与 KikasaGrade 的 uScreenSize 同值

            shader.Parameters["uScreenSize"]?.SetValue(realScreenSize);
            shader.Parameters["uCamX"]?.SetValue(realScreenPos.X);
            shader.Parameters["uCamY"]?.SetValue(realScreenPos.Y);
            shader.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            shader.Parameters["uSpreadProgress"]?.SetValue(kdp.SpreadProgress);
            shader.Parameters["uSpreadOrigin"]?.SetValue(origin);
            shader.Parameters["uMaskTime"]?.SetValue(kdp.EffectTime);
            shader.Parameters["uRain"]?.SetValue(kdp.RainBlend);
            //水侧色板先行值：翻转期取镜面预览的靠拢度，所有的水一拍先行变色、天穹留到白闪；
            //其余时刻恒等 RainBlend，与 uRain 零差异
            float lakeRain = kdp.Phase == KikasaDomainPhase.Flipping
                ? (kdp.FlipToRain
                    ? MathHelper.Max(kdp.RainBlend, kdp.FlipMix)
                    : MathHelper.Min(kdp.RainBlend, 1f - kdp.FlipMix))
                : kdp.RainBlend;
            shader.Parameters["uLakeRain"]?.SetValue(lakeRain);
            shader.Parameters["uFlash"]?.SetValue(flashStrength);
            shader.Parameters["uWaterLevel"]?.SetValue(waterLevel);
            shader.Parameters["uWaterWobble"]?.SetValue(waterWobble);
            //行波源与湖面着色器同一批，垫底顶边跟着水线一起荡
            KikasaDomainDeco.FillWaveUniforms(shader, realScreenPos, realScreenSize);
            //让位坑同批：坑内垫底让位露出天穹，与湖面坑逐像素重合
            KikasaDomainDeco.FillTideUniforms(shader, kdp, realScreenPos, realScreenSize);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            //还原批次复刻 vanilla DrawBG 的精确矩阵，少了平移修正项会在缩放≠1 时偏移后续背景层

            Matrix restore = Main.BackgroundViewMatrix.TransformationMatrix;
            restore.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                * new Vector3(1f, Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, restore);
        }
    }
}
