using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    //场景判定：旧网内激活专属天幕与冷暗滤镜；音乐置 0 = 死寂（考古基调，环境声 M1 再议）
    internal class OldNetSkyScene : ModSceneEffect
    {
        public override int Music => 0;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => OldNetWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(OldNetSky.Name, isActive);
    }

    /// <summary>
    /// 旧网天幕：近黑深空 + 稀疏"未熄灭的服务器"星点。<br/>
    /// 星点按宏观种子确定（同一存档的旧网星空不变），带视差与慢闪；
    /// 少量濒死星呈暗红并以极长周期明灭。纯 CPU 绘制，零 shader 依赖
    /// </summary>
    internal class OldNetSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OldNetSky";

        private bool active;
        private float intensity;

        //星野单元尺寸（像素）与视差系数
        private const int StarCell = 96;
        private const float ParallaxX = 0.10f;
        private const float ParallaxY = 0.06f;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册：缺 Filter 会在 SpecialVisuals 首跑时 NRE
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.012f, 0.035f, 0.045f)
                .UseOpacity(0.18f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.001f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            intensity = MathHelper.Lerp(intensity, active ? 1f : 0f, 0.04f);
        }

        public override float GetCloudAlpha() => 1f - intensity;

        private static float Hash(float x, float y) {
            float h = MathF.Sin(x * 127.1f + y * 311.7f + OldNetMetrics.MacroSeed * 0.001f) * 43758.5453f;
            return h - MathF.Floor(h);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨 0 深度切片只画一次，覆盖所有原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (intensity <= 0.003f) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;
            float t = (float)Main.timeForVisualEffects / 60f;

            //shader 路径：四层天幕（渐变/双层视差星野/数据云/墙侧余晖）一 pass 完成
            Effect sky = EffectLoader.OldNetSky?.Value;
            if (sky != null) {
                //墙右缘屏幕x：远离墙时为大负值，余晖自然消失（与 BlackwallRender 同口径）
                float wallScreenX = Vector2.Transform(
                    new Vector2(OldNetMetrics.WallCols * 16f, 0f) - Main.screenPosition,
                    Main.GameViewMatrix.TransformationMatrix).X;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                sky.Parameters["uTime"]?.SetValue(t);
                sky.Parameters["uIntensity"]?.SetValue(intensity);
                sky.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                sky.Parameters["uCam"]?.SetValue(Main.screenPosition);
                sky.Parameters["uSeed"]?.SetValue(OldNetMetrics.MacroSeed * 0.001f);
                sky.Parameters["uWallScreenX"]?.SetValue(wallScreenX);
                sky.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, new Rectangle(0, 0, vpW, vpH), Color.White);
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.BackgroundViewMatrix.TransformationMatrix);
                return;
            }

            //CPU 回退：渐变带 + 星野双重循环

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //深空渐变：头顶近黑，地平线残留一点冷青灰
            const int bands = 18;
            int bandH = vpH / bands + 1;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                Color c = Color.Lerp(new Color(2, 3, 6), new Color(10, 22, 26), k * k);
                spriteBatch.Draw(px, new Rectangle(0, i * (vpH / bands), vpW, bandH), c * intensity);
            }

            //星野：世界锚定视差 + 确定性哈希，只铺上方 70% 屏幕
            float camX = Main.screenPosition.X * ParallaxX;
            float camY = Main.screenPosition.Y * ParallaxY;
            int cellX0 = (int)MathF.Floor(camX / StarCell) - 1;
            int cellX1 = (int)MathF.Floor((camX + vpW) / StarCell) + 1;
            int cellY0 = (int)MathF.Floor(camY / StarCell) - 1;
            int cellY1 = (int)MathF.Floor((camY + vpH * 0.72f) / StarCell) + 1;

            for (int cx = cellX0; cx <= cellX1; cx++) {
                for (int cy = cellY0; cy <= cellY1; cy++) {
                    float presence = Hash(cx, cy);
                    if (presence < 0.62f) {
                        continue;
                    }
                    float ox = Hash(cx + 17.3f, cy) * StarCell;
                    float oy = Hash(cx, cy + 9.1f) * StarCell;
                    float sx = cx * StarCell + ox - camX;
                    float sy = cy * StarCell + oy - camY;
                    if (sx < -4f || sx > vpW + 4f || sy < -4f || sy > vpH * 0.72f) {
                        continue;
                    }

                    float phase = Hash(cx + 3.7f, cy + 5.9f) * MathHelper.TwoPi;
                    float twinkle = 0.65f + 0.35f * MathF.Sin(t * (0.6f + presence) + phase);
                    //地平衰减：越接近地平线越暗
                    float heightFade = 1f - MathHelper.Clamp(sy / (vpH * 0.72f), 0f, 1f) * 0.55f;

                    Color starColor;
                    float dying = Hash(cx + 31.7f, cy + 71.3f);
                    if (dying > 0.86f) {
                        //濒死服务器：暗红，极长周期明灭，低谷时几乎熄灭
                        float slow = 0.5f + 0.5f * MathF.Sin(t * 0.05f + phase * 3f);
                        starColor = new Color(200, 60, 45) * (slow * slow);
                    }
                    else {
                        starColor = Color.Lerp(new Color(150, 220, 235), new Color(220, 240, 245), presence);
                    }

                    float size = 1f + Hash(cx + 1.1f, cy + 2.2f) * 1.8f;
                    Color final = starColor * (twinkle * heightFade * intensity);
                    spriteBatch.Draw(px, new Vector2(sx, sy), null, final, 0f,
                        new Vector2(px.Width * 0.5f, px.Height * 0.5f),
                        new Vector2(size / px.Width, size / px.Height), SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
