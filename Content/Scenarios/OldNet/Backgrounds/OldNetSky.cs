using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    //场景判定：旧网内激活专属天幕与黑红滤镜；音乐置 0 = 死寂（环境声层另走 OldNetAmbience）
    internal class OldNetSkyScene : ModSceneEffect
    {
        public override int Music => 0;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => OldNetWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(OldNetSky.Name, isActive);
    }

    /// <summary>
    /// 旧网天幕 v2：墙外即墙海（黑墙化）。<br/>
    /// shader 路径承 CyberDomainSky 的黑墙语法（三层视差墙海幕帘/死网线框残骸/
    /// 巨物剪影槽），身份件保留：濒死服务器余烬按宏观种子确定、西缘余晖锚、
    /// 带内腐化分级（墙脚静→衰减区狂）。CPU 回退为同色板渐变+余烬星野
    /// </summary>
    internal class OldNetSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OldNetSky";

        private bool active;
        private float intensity;
        //带内腐化平滑值（目标值按玩家所在列取 OldNetMetrics.CorruptionAt）
        private float corruptSmooth;

        //CPU 回退星野单元尺寸（像素）与视差系数
        private const int StarCell = 96;
        private const float ParallaxX = 0.10f;
        private const float ParallaxY = 0.06f;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册：缺 Filter 会在 SpecialVisuals 首跑时 NRE
            //黑红微染（黑是主体，红只轻推），与赛博领域的酒红滤镜刻意拉开饱和度
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.055f, 0.012f, 0.016f)
                .UseOpacity(0.18f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.001f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            intensity = MathHelper.Lerp(intensity, active ? 1f : 0f, 0.04f);
            //腐化度慢速追随玩家所在列，带界过渡不跳变
            float target = OldNetWorld.Active
                ? OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f)) : 0f;
            corruptSmooth = MathHelper.Lerp(corruptSmooth, target, 0.03f);
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
            //相机捕捉路径另有一套屏幕参数篡改，天幕不入捕捉图
            if (CaptureManager.Instance.IsCapturing) {
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

            //墙右缘屏幕x：远离墙时为大负值，余晖自然消失（与 BlackwallRender 同口径）
            float wallScreenX = Vector2.Transform(
                new Vector2(OldNetMetrics.WallCols * 16f, 0f) - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix).X;

            //shader 路径：墙海幕帘/线框残骸/余烬/巨物一 pass 完成
            Effect sky = EffectLoader.OldNetSky?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (sky != null && noise != null) {
                //DrawBG 窗口内 screenPosition 被加了缩放平移，须还原真实相机值做视差
                Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                sky.Parameters["uTime"]?.SetValue(t);
                sky.Parameters["uIntensity"]?.SetValue(intensity);
                sky.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                sky.Parameters["uCamX"]?.SetValue(realScreenPos.X);
                //纵向视差基准：相机中心相对世界地表的偏移，shader 端 clamp 防极端坐标
                float camYOff = realScreenPos.Y + vpH * 0.5f - (float)Main.worldSurface * 16f;
                sky.Parameters["uCamY"]?.SetValue(camYOff);
                sky.Parameters["uSeed"]?.SetValue(OldNetMetrics.MacroSeed * 0.001f);
                sky.Parameters["uWallScreenX"]?.SetValue(wallScreenX);
                sky.Parameters["uCorrupt"]?.SetValue(corruptSmooth);
                //涌动合成值：常规涌动与大潮前奏取 max（大潮幕一把天幕拉向墙侧）
                sky.Parameters["uSurge"]?.SetValue(OldNetSkyEvents.SurgeComposed);
                sky.Parameters["uGiant"]?.SetValue(new Vector4(
                    OldNetSkyEvents.GiantPos.X, OldNetSkyEvents.GiantPos.Y,
                    OldNetSkyEvents.GiantScale, OldNetSkyEvents.GiantMix));
                //网的注视：注视度 + 玩家屏幕uv（红眼朝向；与 wallScreenX 同口径含缩放）
                sky.Parameters["uWatch"]?.SetValue(OldNetLinkFX.Watch);
                Vector2 playerScreen = Vector2.Transform(
                    Main.LocalPlayer.Center - Main.screenPosition,
                    Main.GameViewMatrix.TransformationMatrix);
                sky.Parameters["uPlayerUv"]?.SetValue(playerScreen / new Vector2(vpW, vpH));
                sky.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, new Rectangle(0, 0, vpW, vpH), Color.White);

                spriteBatch.End();
                //还原批次复刻 vanilla DrawBG 的精确矩阵，少平移修正项会在缩放≠1 时偏移后续背景层
                Matrix restore = Main.BackgroundViewMatrix.TransformationMatrix;
                restore.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                    * new Vector3(1f, Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, restore);
                return;
            }

            //CPU 回退：黑红渐变带 + 余烬星野（同色板，不画幕帘）

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //深渊渐变：头顶近黑，地平线残一点暗红
            const int bands = 18;
            int bandH = vpH / bands + 1;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                Color c = Color.Lerp(new Color(3, 1, 2), new Color(18, 5, 7), k * k);
                spriteBatch.Draw(px, new Rectangle(0, i * (vpH / bands), vpW, bandH), c * intensity);
            }

            //余烬星野：世界锚定视差 + 确定性哈希，只铺上方 70% 屏幕
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
                        //濒死服务器：极长周期明灭，低谷时几乎熄灭
                        float slow = 0.5f + 0.5f * MathF.Sin(t * 0.05f + phase * 3f);
                        starColor = new Color(200, 60, 45) * (slow * slow);
                    }
                    else if (Hash(cx + 43.9f, cy + 12.7f) > 0.98f) {
                        //幸存者冷青：还亮着的老服务器，考古残光
                        starColor = new Color(58, 128, 140);
                    }
                    else {
                        starColor = Color.Lerp(new Color(140, 42, 30), new Color(205, 80, 52), presence);
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
