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

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册，KikasaDomainSystem 按该名驱动，缺 Filter 直接 NRE

            SkyManager.Instance[Name] = this;
            //血暮微滤镜、透明度由 Update 动态驱动

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.10f, 0.02f, 0.03f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || presence > 0.004f;
        public override void Reset() {
            active = false;
            presence = 0f;
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

            //血暮滤镜垫底色（低画质回退时的主要氛围来源）

            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.12f * presence
                * (kdp?.PresenceSmooth ?? 0f));
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

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uSkyAlpha"]?.SetValue(presence);
            //遮罩空间尺寸取视口真实像素，与 KikasaGrade 的 uScreenSize 同值

            shader.Parameters["uScreenSize"]?.SetValue(realScreenSize);
            shader.Parameters["uCamX"]?.SetValue(realScreenPos.X);
            shader.Parameters["uCamY"]?.SetValue(realScreenPos.Y);
            shader.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            shader.Parameters["uSpreadProgress"]?.SetValue(kdp.SpreadProgress);
            shader.Parameters["uSpreadOrigin"]?.SetValue(origin);
            shader.Parameters["uMaskTime"]?.SetValue(kdp.EffectTime);
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
