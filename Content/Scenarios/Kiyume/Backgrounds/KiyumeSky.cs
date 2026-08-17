using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Backgrounds
{
    //场景判定：梦里激活专属天幕与沉红滤镜；音乐置 0——湖畔只有水声
    internal class KiyumeSkyScene : ModSceneEffect
    {
        public override int Music => 0;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => KiyumeWorld.Active && !Main.gameMenu;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(KiyumeSky.Name, isActive);
    }

    /// <summary>
    /// 鬼梦天幕：红黑穹顶 + 双层远山脊 + 地平线上另一座湖畔村的影像。<br/>
    /// 与鬼伞的梦空同源同色板，地平线由 C# 按真实相机与村落基准行折算后喂给着色器
    /// （<see cref="KiyumeMetrics.HorizonRefRow"/>），不让它自己猜
    /// </summary>
    internal class KiyumeSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:KiyumeSky";

        //地平线随相机上下的反向漂移系数：低处看远山更高
        private const float HorizonDrift = 0.000045f;
        private const float HorizonBase = 0.60f;

        private bool active;
        private float presence;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册：缺 Filter 会在 SpecialVisuals 首跑时 NRE
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
            presence = KiyumeAmbienceSystem.Presence;
            //滤镜垫底（着色器缺失时的主要氛围来源）：沉红压场
            Filters.Scene[Name]?.GetShader()
                ?.UseColor(0.13f, 0.02f, 0.02f)
                .UseOpacity(0.17f * presence);
        }

        public override float GetCloudAlpha() => 1f - presence;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (presence <= 0.004f) {
                Filters.Scene[Name]?.GetShader()?.UseOpacity(0f);
                return;
            }
            //跨 0 深度切片只画一次，盖过原版山野远景（也盖过更早绘制的日月）
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (CaptureManager.Instance.IsCapturing) {
                return;
            }
            Effect shader = EffectLoader.KiyumeSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            //DrawBG 窗口内 screenPosition 被加了缩放平移，须还原真实相机值再喂视差
            Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;
            float camCenterY = realScreenPos.Y + vpH * 0.5f;
            float horizon = MathHelper.Clamp(
                HorizonBase - (camCenterY - KiyumeMetrics.HorizonRefWorldY) * HorizonDrift, 0.40f, 0.80f);

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
            shader.Parameters["uHorizon"]?.SetValue(horizon);
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
