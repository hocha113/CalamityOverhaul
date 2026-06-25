using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch
{
    /// <summary>奸奇场景效果</summary>
    internal class TzeentchSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => TzeentchEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(TzeentchSky.Name, isActive);
    }

    /// <summary>
    /// 奸奇魔法领域天空，使用 <c>TzeentchSky.fx</c> 程序化着色器绘制双色魔火/虹彩能量带/
    /// 奥术符文/注视之眼/星尘/脉冲，取代旧的逐像素复合绘制（88 片迷雾贴图 + 16×32 符文叠绘
    /// + 每两帧烟雾粒子），由 GPU 一次性完成
    /// </summary>
    internal class TzeentchSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:TzeentchSky";
        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //紫色魔法滤镜，配合着色器让世界整体染上奸奇紫调
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.2f, 0.1f, 0.3f)//紫色魔法调
                .UseOpacity(0.5f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            //仅在最底层背景绘制一次（minDepth<0 且 maxDepth>=0 的那一层覆盖所有背景）
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }

            Effect shader = EffectLoader.TzeentchSky?.Value;
            if (shader == null) {
                //着色器缺失时回退为深紫纯色叠加，氛围不至于完全丢失
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    new Color(15, 8, 25) * (intensity * 0.95f)
                );
                return;
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uIntensity"]?.SetValue(intensity);
            shader.Parameters["uAspectRatio"]?.SetValue(vpW / (float)vpH);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }

        public override bool IsActive() => active || intensity > 0.001f;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = TzeentchEffect.Cek();

            //平滑淡入淡出
            float target = TzeentchEffect.IsActive ? 1f : 0f;
            intensity = MathHelper.Lerp(intensity, target, 0.05f);
            if (!TzeentchEffect.IsActive && intensity < 0.003f) {
                intensity = 0f;
                Deactivate();
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用魔法紫色调
            if (intensity > 0.1f) {
                float magicR = 0.9f;
                float magicG = 0.7f;
                float magicB = 1.0f;

                Color tintedColor = new Color(
                    (int)(inColor.R * magicR),
                    (int)(inColor.G * magicG),
                    (int)(inColor.B * magicB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.4f);
            }
            return inColor;
        }
    }

    /// <summary>奸奇效果调度：仅负责开关、网络同步与静音，氛围视觉全部交由 TzeentchSky 着色器</summary>
    internal class TzeentchEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private static float origMusicVolume = -1f;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.TzeentchEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.TzeentchEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.TzeentchEffect);
                    packet.Write(IsActive);
                    packet.Send(-1, whoAmI);
                }
            }
        }

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {
                IsActive = false;
                return false;
            }

            return true;
        }

        public override void PostUpdateEverything() {
            if (!Cek()) {
                if (origMusicVolume > 0f) {
                    Main.musicVolume = origMusicVolume;
                    origMusicVolume = -1f;
                }
                return;
            }

            if (++CekTimer > 60 * 60 * 3)//最多持续3分钟
            {
                IsActive = false;
                return;
            }

            //压制音乐，营造奸奇领域的诡异静默
            Main.newMusic = Main.musicBox2 = -1;
            if (Main.musicVolume > 0f) {
                origMusicVolume = Main.musicVolume;
            }
            Main.musicVolume = 0f;
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
