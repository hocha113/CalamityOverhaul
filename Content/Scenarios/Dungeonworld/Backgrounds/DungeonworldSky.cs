using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Backgrounds
{
    //场景判定：地牢子世界内激活专属天幕与暗石蓝灰滤镜；音乐交还原版（ZoneDungeon 免费到位）
    internal class DungeonworldSkyScene : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        //加载菜单期 DrawSetup 自绘,禁止天幕/暗石滤镜在 gameMenu 被 SpecialVisuals 拉起
        public override bool IsSceneEffectActive(Player player) => Dungeonworld.Active && !Main.gameMenu;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(DungeonworldSky.Name, isActive);
    }

    /// <summary>
    /// 地牢子世界天幕：暗石蓝灰永夜基调 + 地平烛金残光，随玩家深度压暗并染当层强调色<br/>
    /// 世界主体在地下（worldSurface 设于顶部），天幕只在天空缓冲带/钟楼段可见；
    /// 全域氛围由同名 Filter 轻染承担（色板与 DungeonworldLoadTheme 同源）
    /// </summary>
    internal class DungeonworldSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:DungeonworldSky";

        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册：缺 Filter 会在 SpecialVisuals 首跑时 NRE
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.030f, 0.046f, 0.085f)
                .UseOpacity(0.22f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.001f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            intensity = MathHelper.Lerp(intensity, active ? 1f : 0f, 0.03f);
        }

        //玩家深度 0..1（0=世界顶，1=世界底），近似层带梯度
        // TODO: 集成期可从生成侧层带表取精确带界
        private static float DepthGrade() {
            if (Main.LocalPlayer == null || Main.maxTilesY <= 0) {
                return 0f;
            }
            return MathHelper.Clamp((float)(Main.LocalPlayer.Center.Y / 16.0 / Main.maxTilesY), 0f, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //minDepth<0 单层背景
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

            float grade = DepthGrade();
            //当层强调色：七段线性混合
            float band = MathHelper.Clamp(grade * 7f, 0f, 6.999f);
            int bi = (int)band;
            Color accent = Color.Lerp(
                DungeonworldLoadTheme.BandAccents[bi],
                DungeonworldLoadTheme.BandAccents[Math.Min(bi + 1, DungeonworldLoadTheme.BandCount - 1)],
                band - bi);

            var shader = EffectLoader.DungeonworldSky?.Value;
            spriteBatch.End();
            if (shader != null) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

                //uniform 残值硬规则：全参数重设
                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
                shader.Parameters["uIntensity"]?.SetValue(intensity);
                shader.Parameters["uAspectRatio"]?.SetValue((float)vpW / vpH);
                shader.Parameters["uDepthGrade"]?.SetValue(grade);
                shader.Parameters["uAccent"]?.SetValue(DungeonworldLoadTheme.Vec3(accent));
                shader.CurrentTechnique.Passes[0].Apply();

                spriteBatch.Draw(px, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //CPU 回退：倒置明度带状渐变（头顶近黑，地平残光），不落回原版亮蓝天
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                const int bands = 20;
                int bandH = vpH / bands + 1;
                for (int i = 0; i < bands; i++) {
                    float t = i / (float)(bands - 1);
                    Color c = Color.Lerp(DungeonworldLoadTheme.Abyss, DungeonworldLoadTheme.Stone * 0.85f, t * t);
                    c = Color.Lerp(c, accent, 0.06f);
                    c = Color.Lerp(c, Color.Black, grade * 0.4f);
                    spriteBatch.Draw(px, new Rectangle(0, i * (vpH / bands), vpW, bandH), c * intensity);
                }
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
