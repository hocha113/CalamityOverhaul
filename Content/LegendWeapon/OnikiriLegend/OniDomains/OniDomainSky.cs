using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    //鬼域激活期间（表里都算）启用天空替换
    internal class OniDomainSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && (OniDomain.Local?.AnyActive ?? false);
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(OniDomainSky.Name, isActive);
    }

    /// <summary>
    /// 鬼域双世界天空：表=逢魔黄昏，里=淡底浓墨，山脊几何表里同构
    /// <br/>在场强度随开收域墨水进度，表里切换用快速过渡（剥落纸层作掩护）
    /// </summary>
    internal class OniDomainSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:OniDomainSky";

        private bool active;
        //天空在场 0~1，跟随墨水覆盖
        private float presence;
        //表里调色板过渡 0~1，比 UraSmooth 快，赶在纸层揭开前完成
        private float uraBlend;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //ManageSpecialBiomeVisuals 对 Filters.Scene[name] 不做空检查，
            //Sky 与 Filter 必须同名成对注册，缺 Filter 直接 NRE
            SkyManager.Instance[Name] = this;
            //冷暗微滤镜：透明度由 Update 动态驱动，仅里世界生效
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.03f, 0.03f, 0.06f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || presence > 0.004f;
        public override void Reset() {
            active = false;
            presence = 0f;
            uraBlend = 0f;
        }

        public override void Update(GameTime gameTime) {
            OniDomainPlayer odp = OniDomain.Local;

            float uraTarget = 0f;
            if (odp != null && odp.AnyActive) {
                //空间浸染遮罩负责揭示，presence 直接快速就位
                presence = MathHelper.Lerp(presence, 1f, 0.45f);
                uraTarget = odp.WorldIsUra ? 1f : 0f;
            }
            else {
                //域已闭：收域末帧遮罩本就全遮，直接归零防止 mode=0 后整屏闪回
                presence = 0f;
            }
            //快速过渡：纸层剥落前段全覆盖能遮住切换，须赶在两半分开前基本走完
            uraBlend = MathHelper.Lerp(uraBlend, uraTarget, 0.16f);

            //里世界滤镜垫底色（低画质回退时的主要氛围来源）
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.10f * uraBlend * presence);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨 0 深度切片只画一次：该切片在所有原版背景层之后绘制，覆盖山野等背景
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
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

            OniDomainPlayer odp = OniDomain.Local;
            bool spread = odp != null
                && (odp.Phase == OniDomainPhase.Opening || odp.Phase == OniDomainPhase.Closing);
            //原点与遮罩噪声时间都必须与 OniWorldGrade 完全同源（含 zoom 变换），
            //两个着色器的浸染前沿才能在任意缩放下逐像素重合
            Vector2 origin = odp != null
                ? Vector2.Transform(odp.EyeWorldPos - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix)
                : Vector2.Zero;
            float maskTime = odp?.EffectTime ?? 0f;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uSkyAlpha"]?.SetValue(presence);
            shader.Parameters["uUraBlend"]?.SetValue(uraBlend);
            //遮罩空间尺寸与 OniWorldGrade 严格同源，不用视口尺寸
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["uCamX"]?.SetValue(Main.screenPosition.X);
            shader.Parameters["uCamY"]?.SetValue(Main.screenPosition.Y);
            shader.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            shader.Parameters["uSpreadProgress"]?.SetValue(odp?.SpreadProgress ?? 0f);
            shader.Parameters["uSpreadOrigin"]?.SetValue(origin);
            shader.Parameters["uMaskTime"]?.SetValue(maskTime);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
