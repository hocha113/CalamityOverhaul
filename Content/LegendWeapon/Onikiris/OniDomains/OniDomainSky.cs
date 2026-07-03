using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    //里世界激活天空替换
    internal class OniDomainSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && OniDomain.LocalUraSmooth > 0.01f;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(OniDomainSky.Name, isActive);
    }

    /// <summary>里世界天空：墨色天穹、苍白圆月、鸟居剪影，强度随 UraSmooth</summary>
    internal class OniDomainSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OniDomainSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //ManageSpecialBiomeVisuals 对 Filters.Scene[name] 不做空检查，
            //Sky 与 Filter 必须同名成对注册，缺 Filter 直接 NRE
            SkyManager.Instance[Name] = this;
            //冷暗微滤镜：主调色由 OniWorldGrade 负责，这里只垫低画质回退时的底色
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.03f, 0.03f, 0.06f)
                .UseOpacity(0.12f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || OniDomain.LocalUraSmooth > 0.004f;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) { }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //只绘制一次，最底背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float ura = OniDomain.LocalUraSmooth;
            if (ura <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.OniSky?.Value;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
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
            shader.Parameters["uUra"]?.SetValue(ura);
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
            shader.Parameters["uCamX"]?.SetValue(Main.screenPosition.X);
            shader.Parameters["uCamY"]?.SetValue(Main.screenPosition.Y);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
