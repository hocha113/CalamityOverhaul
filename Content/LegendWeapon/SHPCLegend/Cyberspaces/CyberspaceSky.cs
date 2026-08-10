using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 赛博领域 L3 专属天空(红黑数据深渊)。玩家主动能力，不走 ModSceneEffect 场景竞争，
    /// 由 <see cref="CyberspaceSystem"/> 每帧按 <see cref="Cyberspace.ViewedTakeover"/> 手动驱动激活
    /// </summary>
    internal class CyberspaceSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:CyberspaceSky";

        private bool active;
        //天空在场 0~1，追随观看域的接管强度
        private float presence;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册，缺 Filter 则 SpecialVisuals 路径 NRE
            SkyManager.Instance[Name] = this;
            //暗红微滤镜，透明度由 Update 动态驱动（低画质/简约模式下的主要氛围来源）
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.10f, 0.02f, 0.03f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        //激活状态只看 active，淡出尾巴由 presence 兜住（激活短路法则）
        public override bool IsActive() => active || presence > 0.004f;
        public override void Reset() {
            active = false;
            presence = 0f;
        }

        public override void Update(GameTime gameTime) {
            float target = Cyberspace.ViewedTakeover;
            presence = MathHelper.Lerp(presence, target, 0.20f);
            if (presence < 0.003f && target <= 0f) {
                presence = 0f;
            }
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.12f * presence);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (presence <= 0.004f) {
                return;
            }
            //跨 0 深度切片只画一次，覆盖所有原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            //简约偏好：跳过天幕本体，只留 Filter 轻染与光照压暗
            if (DomainVisuals.Concise) {
                return;
            }
            //相机捕捉路径另有一套屏幕参数篡改，域天空不入捕捉图
            if (CaptureManager.Instance.IsCapturing) {
                return;
            }
            Effect shader = EffectLoader.CyberDomainSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            //DrawBG 窗口内 screenPosition 被加了缩放平移，须还原真实相机值做视差
            Vector2 realScreenPos = Main.screenPosition - Main.BackgroundViewMatrix.Translation;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uPresence"]?.SetValue(presence);
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
            shader.Parameters["uCamX"]?.SetValue(realScreenPos.X);
            //纵向视差基准：相机中心相对世界地表的偏移，shader 端 clamp 防极端坐标
            float camYOff = realScreenPos.Y + vpH * 0.5f - (float)Main.worldSurface * 16f;
            shader.Parameters["uCamY"]?.SetValue(camYOff);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            //还原批次复刻 vanilla DrawBG 的精确矩阵，少平移修正项会在缩放≠1 时偏移后续背景层
            Matrix restore = Main.BackgroundViewMatrix.TransformationMatrix;
            restore.Translation -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                * new Vector3(1f, Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically) ? -1f : 1f, 1f);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, restore);
        }
    }
}
