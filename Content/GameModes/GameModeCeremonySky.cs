using CalamityOverhaul.Content.GameModes.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    //切换演出期间启用临时天色
    internal class GameModeCeremonySceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && GameModeCeremonySky.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(GameModeCeremonySky.Name, isActive);
    }

    /// <summary>
    /// 模式切换演出的临时天色：随大字包络快起缓收，滤镜与天顶晕染取表现脸主色，
    /// 关闭向减半浓度并偏冷收灰。<br/>
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// 各端读本地 <see cref="GameModeCeremony"/> 状态，纯本地演出量，无网络
    /// </summary>
    internal class GameModeCeremonySky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:GameModeCeremony";

        private bool active;

        /// <summary>演出包络 0..1：快起（前 12%）、持续、随大字淡出（后 25%）</summary>
        internal static float Envelope {
            get {
                if (!GameModeCeremony.LineActive) {
                    return 0f;
                }
                float t = GameModeCeremony.LineProgress;
                float aIn = MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.12f, 0f, 1f));
                float aOut = MathHelper.SmoothStep(0f, 1f, Math.Clamp((1f - t) / 0.25f, 0f, 1f));
                float level = aIn * aOut;
                //关闭向演出收敛：浓度减半
                return GameModeCeremony.LineEnabled ? level : level * 0.5f;
            }
        }

        /// <summary>仍需在场（含渐出尾巴）</summary>
        internal static bool Visible => Envelope > 0.004f;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0f, 0f, 0f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            float env = Envelope;
            //全屏滤镜只给低浓度基调，主戏在天顶晕染
            Filters.Scene[Name]?.GetShader()?
                .UseColor(TintColor().ToVector3() * 0.16f)
                .UseOpacity(0.24f * env);
        }

        /// <summary>演出天色：开启向=表现脸主色，关闭向=偏冷收灰</summary>
        private static Color TintColor() {
            Color accent = GameModeTheme.Accent(GameModeCeremony.LineFace);
            if (!GameModeCeremony.LineEnabled) {
                accent = Color.Lerp(accent, new Color(90, 100, 120), 0.55f);
            }
            return accent;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，压在原版背景之上
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float env = Envelope;
            if (env <= 0.004f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;
            Color accent = TintColor();

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //自天顶向下的主色晕染：二次衰减的条带渐层，天色被短暂夺色
            int reach = (int)(vpH * 0.62f);
            const int bands = 30;
            int bandH = Math.Max(1, reach / bands);
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                float fall = (1f - k) * (1f - k);
                Color c = accent * (env * 0.30f * fall);
                spriteBatch.Draw(white, new Rectangle(0, i * bandH, vpW, bandH), c);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
