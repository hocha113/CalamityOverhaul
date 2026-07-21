using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.Narrative;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 拔刀后逢魔黄昏，贯穿化樱与 <see cref="FirstMetHimayo"/>，落幕后渐出<br/>
    /// 纯本地，保持条件逐帧轮询，无粘滞
    /// </summary>
    internal static class ToriiDusk
    {
        //渐入约0.75s，渐出约1.5s
        private const float FadeInPerTick = 1f / 45f;
        private const float FadeOutPerTick = 1f / 90f;

        /// <summary>在场强度0~1</summary>
        public static float Intensity { get; private set; }

        /// <summary>仍需在场(含渐出尾巴)</summary>
        public static bool Visible => Intensity > 0.004f;

        internal static void Update() {
            Intensity = MathHelper.Clamp(
                Intensity + (Hold() ? FadeInPerTick : -FadeOutPerTick), 0f, 1f);
        }

        internal static void Reset() => Intensity = 0f;

        /// <summary>退场中或初见在播则保持，鬼域激活时让位</summary>
        private static bool Hold() {
            if (Main.dedServ || Main.gameMenu) {
                return false;
            }
            if (OniDomain.Local?.AnyActive ?? false) {
                return false;
            }
            return ToriiShrineActor.DepartureHoldingStage
                || NarrativeRouter.IsActive<FirstMetHimayo>();
        }
    }

    internal class ToriiDuskSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            ToriiDusk.Update();
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            ToriiDusk.Reset();
        }

        //日光向暖金收拢
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float dusk = ToriiDusk.Intensity;
            if (dusk <= 0.001f) {
                return;
            }
            Color duskTile = new(255, 178, 112);
            Color duskBg = new(172, 116, 78);
            tileColor = Color.Lerp(tileColor, duskTile, dusk * 0.42f);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, dusk * 0.42f);
        }
    }

    //黄昏在场期间启用天空替换
    internal class ToriiDuskSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && ToriiDusk.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(ToriiDuskSky.Name, isActive);
    }

    /// <summary>
    /// 逢魔黄昏天空，复用 <see cref="EffectLoader.OniSky"/> 表世界调色板(uUraBlend=0)，全覆盖(uSpreadMode=0)<br/>
    /// 强度由 <see cref="ToriiDusk.Intensity"/> 驱动
    /// </summary>
    internal class ToriiDuskSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:ToriiDuskSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //ManageSpecialBiomeVisuals 对 Filters.Scene[name] 不做空检查，
            //Sky 与 Filter 必须同名成对注册，缺 Filter 直接 NRE；
            //滤镜顺带极淡暖金罩
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.30f, 0.20f, 0.06f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        //IsActive 只反映 SkyManager 激活态。ManageSpecialBiomeVisuals 只在
        //inZone != IsActive() 时才 Activate。若把外部强度并进判定，首次激活时
        //IsActive 已为 true，Activate 被短路，天空永远进不了活跃列表（实测踩坑）
        //渐出尾巴由 ToriiDusk.Visible 兜住，强度排干后才 Deactivate
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.10f * ToriiDusk.Intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次，盖住原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float presence = ToriiDusk.Intensity;
            if (presence <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.OniSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uSkyAlpha"]?.SetValue(presence);
            shader.Parameters["uUraBlend"]?.SetValue(0f);
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["uCamX"]?.SetValue(Main.screenPosition.X);
            shader.Parameters["uCamY"]?.SetValue(Main.screenPosition.Y);
            shader.Parameters["uSpreadMode"]?.SetValue(0f);
            shader.Parameters["uSpreadProgress"]?.SetValue(0f);
            shader.Parameters["uSpreadOrigin"]?.SetValue(Vector2.Zero);
            shader.Parameters["uMaskTime"]?.SetValue(0f);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
