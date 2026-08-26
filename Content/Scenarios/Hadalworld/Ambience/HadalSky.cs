using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Ambience
{
    //场景判定:海沟内激活深海天幕;不覆写音乐(音频不在 C 路范围)
    internal class HadalSkyScene : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => Hadalworld.Active || HadalAmbience.ForceEnable;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(HadalSky.Name, isActive);
    }

    /// <summary>
    /// 深海天幕:子世界背景按分带渐变的深海色(远景水团),水面以上 alpha 归零
    /// 保留原版海天。色值全部取自 <see cref="HadalDepthProfile.SkyColor"/>,
    /// 深度换算走 Metrics(brief §2 协议)。CPU 回退为同色板渐变条带
    /// </summary>
    internal class HadalSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:HadalSky";

        private bool active;
        private float intensity;
        //相机潜没度:海面以上保留原版云与海天,线下才接管
        private float submerge;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册:缺 Filter 会在 SpecialVisuals 首跑时 NRE。
            //此深蓝浅染兼作捕捉模式与合成滤镜缺席时的底色回退,不透明度在 Update 随深度轻推
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.012f, 0.045f, 0.065f)
                .UseOpacity(0.06f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        //IsActive 只反映激活态,淡出尾巴由 intensity 承接(激活短路陷阱,ToriiDusk 2026-07)
        public override bool IsActive() => active || intensity > 0.001f;
        public override void Reset() {
            active = false;
            intensity = 0f;
            submerge = 0f;
        }

        public override void Update(GameTime gameTime) {
            intensity = MathHelper.Lerp(intensity, active ? 1f : 0f, 0.05f);
            float camRow = (Main.screenPosition.Y + Main.screenHeight * 0.5f) / 16f;
            float target = MathHelper.Clamp((camRow - HadalworldMetrics.SeaLevelRow) / 30f, 0f, 1f);
            submerge = MathHelper.Lerp(submerge, target, 0.1f);

            //配对浅染:随相机深度 0.05→0.13 轻推;合成滤镜在场时让位一半(避免双重压暗)
            float frac = HadalworldMetrics.DepthFraction(camRow * 16f);
            float op = MathHelper.Lerp(0.05f, 0.13f, MathHelper.Clamp(frac * 1.6f, 0f, 1f)) * submerge;
            if (EffectLoader.HadalWater?.Value != null) {
                op *= 0.5f;
            }
            Filters.Scene[Name]?.GetShader()?.UseOpacity(op * intensity);
        }

        //潜没后隐云,近水面保留原版云(海面以上保持海面天空)
        public override float GetCloudAlpha() => 1f - intensity * submerge;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨 0 深度切片只画一次,覆盖所有原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            if (intensity <= 0.003f) {
                return;
            }
            //相机捕捉路径另有一套屏幕参数篡改,天幕不入捕捉图
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

            //屏顶/屏底世界行→分带背景色(远景水团色,比悬浮纱更暗)
            float zoomY = MathHelper.Max(Main.GameViewMatrix.Zoom.Y, 0.01f);
            float topRow = Main.screenPosition.Y / 16f;
            float bottomRow = (Main.screenPosition.Y + vpH / zoomY) / 16f;
            Color colTop = HadalDepthProfile.SkyColor(HadalworldMetrics.DepthFraction(topRow * 16f));
            Color colBottom = HadalDepthProfile.SkyColor(HadalworldMetrics.DepthFraction(bottomRow * 16f));

            //海面线的屏幕 uv(线上 alpha 归零保留原版海天;线远在屏顶上方时为负,全屏接管)
            float seaScreenY = Vector2.Transform(
                new Vector2(0f, HadalworldMetrics.SeaLevelRow * 16f) - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix).Y;
            float seaLineUv = seaScreenY / vpH;

            Effect sky = EffectLoader.HadalSky?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (sky != null && noise != null && !noise.IsDisposed) {
                //DrawBG 窗口内 screenPosition 被加了缩放平移,须还原真实相机值做视差锚
                Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;
                //日光带残余光柱强度与水团斑驳幅度按相机中部深度取
                float midFrac = HadalworldMetrics.DepthFraction(
                    (Main.screenPosition.Y + vpH * 0.5f / zoomY));
                HadalGradeKey mid = HadalDepthProfile.Sample(midFrac);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                sky.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
                sky.Parameters["uIntensity"]?.SetValue(intensity);
                sky.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                sky.Parameters["uColTop"]?.SetValue(colTop.ToVector3());
                sky.Parameters["uColBottom"]?.SetValue(colBottom.ToVector3());
                sky.Parameters["uSeaLineUv"]?.SetValue(seaLineUv);
                //远景视差 0.5:水团比玩家所在水层更"远"
                sky.Parameters["uNoiseAnchor"]?.SetValue(realScreenPos * 0.5f);
                sky.Parameters["uWorldPerPx"]?.SetValue(0.5f / zoomY);
                //远景光柱只活在浅带(与滤镜光束同源渐灭),斑驳深处略增防死黑一块
                sky.Parameters["uShaftStrength"]?.SetValue(mid.Rays * 0.30f);
                sky.Parameters["uShaftTint"]?.SetValue(new Vector3(0.22f, 0.40f, 0.42f));
                sky.Parameters["uDeepMottle"]?.SetValue(MathHelper.Lerp(0.10f, 0.16f,
                    MathHelper.Clamp(midFrac * 2f, 0f, 1f)));
                sky.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, new Rectangle(0, 0, vpW, vpH), Color.White);

                spriteBatch.End();
                //收批即还槽:防同帧邻居吃到陈旧噪声(槽位归还规约 2026-08-26)
                gd.Textures[1] = null;
                //还原批次复刻 vanilla DrawBG 的精确矩阵(镜像 OldNetSky,缩放≠1 时防偏移)
                Matrix restore = Main.BackgroundViewMatrix.TransformationMatrix;
                restore.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                    * new Vector3(1f, Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, restore);
                return;
            }

            //CPU 回退:同色板渐变条带,只画海面线以下
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            const int bands = 20;
            float y0 = MathHelper.Clamp(seaScreenY, 0f, vpH);
            if (y0 < vpH - 2f) {
                float bandH = (vpH - y0) / bands + 1f;
                for (int i = 0; i < bands; i++) {
                    float k = i / (float)(bands - 1);
                    Color c = Color.Lerp(colTop, colBottom, k) * intensity;
                    spriteBatch.Draw(px, new Rectangle(0, (int)(y0 + i * (vpH - y0) / bands), vpW, (int)bandH), c);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
