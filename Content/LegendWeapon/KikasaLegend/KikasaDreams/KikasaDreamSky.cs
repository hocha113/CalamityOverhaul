using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦天空：红黑穹顶与远处村落的影像。与血暮天空同法手动驱动
    /// （<see cref="KikasaDomainSystem"/> 每帧按 DreamBlend 激活），
    /// 交叉渐变全靠 <see cref="KikasaDomainPlayer.DreamBlend"/>——
    /// 拉入结算的红闪掩护下梦空浮现，归返时同路退场
    /// </summary>
    internal class KikasaDreamSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:KikasaDreamSky";

        private bool active;
        //天空在场 0~1，直接吃 DreamBlend（它本身就是包络）

        private float presence;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册，缺 Filter 直接 NRE

            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.13f, 0.02f, 0.02f)
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
            presence = KikasaDomain.ViewedDreamBlend;

            //滤镜垫底（低画质回退时的主要氛围来源）：沉红压场
            Filters.Scene[Name]?.GetShader()
                ?.UseColor(0.13f, 0.02f, 0.02f)
                .UseOpacity(0.17f * presence);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (presence <= 0.004f) {
                Filters.Scene[Name]?.GetShader()?.UseOpacity(0f);
                return;
            }
            //跨 0 深度切片只画一次，盖过原版山野远景

            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (CaptureManager.Instance.IsCapturing) {
                return;
            }
            Effect shader = EffectLoader.KikasaDreamSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            //DrawBG 窗口内 screenPosition 被加了缩放平移，须还原真实相机值再喂视差

            Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uSkyAlpha"]?.SetValue(presence);
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
            shader.Parameters["uCamX"]?.SetValue(realScreenPos.X);
            shader.Parameters["uCamY"]?.SetValue(realScreenPos.Y);
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
