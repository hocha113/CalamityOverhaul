using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>极光带在场期间启用天空替换（只对本机玩家，纯美观甜头）</summary>
    internal class FrostveilAuroraSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Environment;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && FrostveilAmbience.AuroraVisible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(FrostveilAuroraSky.Name, isActive);
    }

    /// <summary>
    /// 极光带：晴夜雪原低频出现的缎带彩光，横陈天际缓慢摆动。
    /// 无着色器，纯精灵合成：Extra_98 真 alpha 梭形作光柱帘（两端自然收口），
    /// SoftGlow 作缎带底光，加色批叠加，强度由 <see cref="FrostveilAmbience.AuroraIntensity"/> 驱动。
    /// Sky 与 Filter 同名成对注册（ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE）；
    /// IsActive 只反映激活态，渐出尾巴由 AuroraVisible 兜住
    /// </summary>
    internal class FrostveilAuroraSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:FrostveilAuroraSky";

        /// <summary>每条缎带的光柱数</summary>
        private const int ShaftCount = 26;

        private static readonly Color AuroraGreen = new(70, 235, 150);
        private static readonly Color AuroraPurple = new(158, 112, 250);
        private static readonly Color AuroraTeal = new(88, 210, 245);

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.04f, 0.1f, 0.09f)
                .UseOpacity(0f), EffectPriority.Low);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            //极冷夜的一点青调，随极光强度呼吸
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.05f * FrostveilAmbience.AuroraIntensity);
        }

        private static float Hash01(int i, float seed) {
            float f = MathF.Sin(i * 12.9898f + seed * 7.13f) * 43758.5453f;
            return f - MathF.Floor(f);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，叠在原版星空之上
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float intensity = FrostveilAmbience.AuroraIntensity;
            if (intensity <= 0.004f) {
                return;
            }
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (spindle == null || glow == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = FrostveilAmbience.AuroraSeed;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Vector2 spindleOrigin = spindle.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            //两条缎带：近带清晰、远带更淡更高，层间差速给纵深
            for (int ribbon = 0; ribbon < 2; ribbon++) {
                float depthK = ribbon == 0 ? 1f : 0.62f;
                float baseY = vpH * (0.16f + 0.085f * ribbon);
                float spacing = (vpW + 160f) / (ShaftCount - 1);
                //视差随缎带层不同，光柱按间距取模无缝循环
                float parallax = Main.screenPosition.X * (0.05f - 0.018f * ribbon) % spacing;

                for (int i = 0; i < ShaftCount; i++) {
                    float x = -80f + i * spacing - parallax;
                    if (x < -120f) {
                        x += spacing * ShaftCount;
                    }
                    float h1 = Hash01(i + ribbon * 57, seed);
                    float p = x * 0.012f + seed + ribbon * 2.4f;

                    //帘脚沿正弦缓慢起伏，柱高低频呼吸+个体差
                    float footY = baseY
                        + MathF.Sin(p * 0.9f + time * 0.3f) * 42f
                        + MathF.Sin(p * 0.37f - time * 0.11f) * 26f;
                    float height = (150f + 190f * (0.5f + 0.5f * MathF.Sin(p * 1.7f + time * 0.42f)))
                        * (0.8f + 0.4f * h1) * depthK;
                    float width = (34f + 22f * h1) * depthK;

                    //色相沿带走：绿→紫，远带偏青
                    float mix = 0.5f + 0.5f * MathF.Sin(p * 0.8f + time * 0.2f);
                    Color tint = Color.Lerp(AuroraGreen, ribbon == 0 ? AuroraPurple : AuroraTeal, mix);

                    //极光微闪：逐柱异相明灭
                    float shimmer = 0.55f + 0.45f * MathF.Sin(p * 2.3f - time * 0.9f + h1 * 6f);
                    float alpha = 0.15f * intensity * shimmer * depthK;
                    if (alpha < 0.004f) {
                        continue;
                    }

                    //柱体：梭形中心抬离帘脚，下端自然衰减吻在帘脚线上
                    Vector2 center = new(x, footY - height * 0.34f);
                    spriteBatch.Draw(spindle, center, null, tint * alpha, 0f, spindleOrigin,
                        new Vector2(width / 72f, height / 72f * 1.9f), SpriteEffects.None, 0f);
                    //帘脚亮芯：极光最亮的一线在下缘
                    spriteBatch.Draw(spindle, new Vector2(x, footY - height * 0.12f), null,
                        tint * (alpha * 0.9f), 0f, spindleOrigin,
                        new Vector2(width * 0.55f / 72f, height * 0.65f / 72f), SpriteEffects.None, 0f);
                }

                //缎带底光：几团宽扁软光沿帘脚铺开
                for (int g = 0; g < 4; g++) {
                    float gx = vpW * (0.14f + 0.24f * g)
                        + MathF.Sin(time * 0.16f + g * 2.1f + seed) * 60f;
                    float gy = baseY + MathF.Sin(gx * 0.012f * 0.9f + time * 0.3f) * 40f;
                    float gmix = 0.5f + 0.5f * MathF.Sin(g * 1.9f + time * 0.18f + seed);
                    Color gtint = Color.Lerp(AuroraGreen, AuroraTeal, gmix);
                    spriteBatch.Draw(glow, new Vector2(gx, gy), null,
                        gtint * (0.09f * intensity * depthK), 0f, glowOrigin,
                        new Vector2(vpW * 0.3f / 64f, 1.1f), SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
