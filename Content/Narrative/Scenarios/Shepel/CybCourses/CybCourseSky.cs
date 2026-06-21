using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    //场景效果注册，当玩家处于CybCourse子世界时激活天空
    internal class CybCourseSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => CybCourseWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(CybCourseSky.Name, isActive);
    }

    //超梦沉浸空间天空，使用着色器绘制程序化深空背景
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
            //深蓝暗色滤镜，配合着色器让世界整体偏冷暗
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
            //只绘制一次，图层最底层（minDepth<0且maxDepth>=0覆盖所有背景层）
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

            float time = (float)Main.timeForVisualEffects * 0.058f;
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["uIntensity"]?.SetValue(intensity);
            shader.Parameters["uAspectRatio"]?.SetValue((float)vpW / vpH);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
