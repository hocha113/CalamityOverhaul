using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal class CybCourseSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => CybCourseWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(CybCourseSky.Name, isActive);
    }

    //CybCourse子世界天空着色器
    internal class CybCourseSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:CybCourseSky";

        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            //深蓝滤镜+着色器
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.02f, 0.04f, 0.10f)
                .UseOpacity(0.20f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.001f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            intensity = MathHelper.Lerp(intensity, active ? 1f : 0f, 0.025f);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (!CybCourse.IsActive) {
                return;
            }
            //minDepth<0单层背景
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            var shader = EffectLoader.CybCourseSky?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //秒制时间，shader 内常量全按 Hz 标定
            float time = (float)Main.timeForVisualEffects / 60f;
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["uIntensity"]?.SetValue(intensity);
            shader.Parameters["uAspectRatio"]?.SetValue((float)vpW / vpH);
            //相机偏移按视口高归一喂层间视差：X 用于横向漂移，
            //Y 以甲板行走面为锚——飞离甲板时构造核心按远景档位下沉，钉在世界里
            shader.Parameters["uCamX"]?.SetValue(Main.screenPosition.X / vpH);
            float camCenterY = Main.screenPosition.Y + vpH * 0.5f;
            float anchorY = CybCourseGen.SurfaceY * 16f;
            shader.Parameters["uCamY"]?.SetValue((camCenterY - anchorY) / vpH);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
