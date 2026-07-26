using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>领域天空。玩家主动能力，不走 ModSceneEffect 场景竞争，由 <see cref="OniDomainSystem"/> 每帧手动驱动激活</summary>
    internal class OniDomainSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OniDomainSky";

        private bool active;
        //天空在场 0~1，跟随墨水覆盖

        private float presence;
        //表里调色板过渡 0~1，比 UraSmooth 快，赶在纸层揭开前完成

        private float uraBlend;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册，OniDomainSystem 按该名驱动，缺 Filter 直接 NRE

            SkyManager.Instance[Name] = this;
            //冷暗微滤镜、透明度由 Update 动态驱动，仅里世界生效

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.03f, 0.03f, 0.06f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || presence > 0.004f;
        public override void Reset() {
            active = false;
            presence = 0f;
            uraBlend = 0f;
        }

        public override void Update(GameTime gameTime) {
            OniDomainPlayer odp = OniDomain.Local;

            float uraTarget = 0f;
            if (odp != null && odp.AnyActive) {
                //空间浸染遮罩负责揭示，presence 直接快速就位

                presence = MathHelper.Lerp(presence, 1f, 0.45f);
                uraTarget = odp.WorldIsUra ? 1f : 0f;
            }
            else {
                //域已闭、Update 排空缓存，Draw 同帧按实时状态截断

                presence = 0f;
            }
            //快速过渡、纸层剥落前段全覆盖能遮住切换，须赶在两半分开前基本走完

            uraBlend = MathHelper.Lerp(uraBlend, uraTarget, 0.16f);

            //里世界滤镜垫底色（低画质回退时的主要氛围来源）

            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.10f * uraBlend * presence);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            OniDomainPlayer odp = OniDomain.Local;
            if (odp == null || !odp.AnyActive) {
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
            Effect shader = EffectLoader.OniSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            //DrawBG 窗口内 screenWidth/Height 被除以背景缩放、screenPosition 加了缩放平移(Main.DoDraw)

            //须还原真实相机值，浸染圆才能与 OniWorldGrade/红环在任意缩放下逐像素重合

            Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;
            Vector2 realScreenSize = new(vpW, vpH);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            bool spread = odp.Phase == OniDomainPhase.Opening || odp.Phase == OniDomainPhase.Closing;
            //原点与遮罩噪声时间都必须与 OniWorldGrade 同源(GameViewMatrix 未被背景窗口篡改，可直接用)

            Vector2 origin = Vector2.Transform(
                odp.EyeWorldPos - realScreenPos,
                Main.GameViewMatrix.TransformationMatrix);
            float maskTime = odp.EffectTime;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uSkyAlpha"]?.SetValue(presence);
            shader.Parameters["uUraBlend"]?.SetValue(uraBlend);
            //遮罩空间尺寸取视口真实像素，与 OniWorldGrade 的 uScreenSize 同值

            shader.Parameters["uScreenSize"]?.SetValue(realScreenSize);
            shader.Parameters["uCamX"]?.SetValue(realScreenPos.X);
            shader.Parameters["uCamY"]?.SetValue(realScreenPos.Y);
            shader.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            shader.Parameters["uSpreadProgress"]?.SetValue(odp.SpreadProgress);
            shader.Parameters["uSpreadOrigin"]?.SetValue(origin);
            shader.Parameters["uMaskTime"]?.SetValue(maskTime);
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
