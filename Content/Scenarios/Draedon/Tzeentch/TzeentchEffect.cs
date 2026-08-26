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

    /// <summary>TzeentchSky.fx程序化天空</summary>
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

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.2f, 0.1f, 0.3f)
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
            //仅minDepth<0且maxDepth>=0层绘制
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }

            Effect shader = EffectLoader.TzeentchSky?.Value;
            if (shader == null) {
                //着色器缺失则深紫纯色回退
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

            float target = TzeentchEffect.IsActive ? 1f : 0f;
            intensity = MathHelper.Lerp(intensity, target, 0.05f);
            if (!TzeentchEffect.IsActive && intensity < 0.003f) {
                intensity = 0f;
                Deactivate();
            }
        }

        public override Color OnTileColor(Color inColor) {
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

    /// <summary>
    /// 奸奇灭声认领：叙事场景档最高子权重（灭声必须压过同档曲目类认领）。
    /// musicVolume 压 0 与还原由仲裁器统一做；3 分钟守护栏迁自旧 CekTimer，
    /// 超时只解除灭声，天空视觉由 FirstMetTzeentch 演出生命周期收束
    /// </summary>
    internal sealed class TzeentchMusicClaim : MusicClaim
    {
        public override MusicTier Tier => MusicTier.NarrativeScene;
        public override int SubWeight => 30;
        public override bool MuteAll => true;
        public override int HardTimeoutFrames => 60 * 60 * 3;
        public override bool ShouldPlay() => TzeentchEffect.IsActive;
        public override int GetMusicSlot() => -1;
    }

    /// <summary>奸奇效果开关与同步</summary>
    internal class TzeentchEffect : ModSystem
    {
        public static bool IsActive;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<TzeentchEffectNet>();
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
            IsActive = reader.ReadBoolean();
            if (VaultUtils.isServer) {
                ModPacket packet = CWRNetWork.GetPacket<TzeentchEffectNet>();
                packet.Write(IsActive);
                packet.Send(-1, whoAmI);
            }
        }

        public static bool Cek() {
            if (!IsActive) {
                return false;
            }

            if (Main.gameMenu) {
                IsActive = false;
                return false;
            }

            return true;
        }

        public override void Unload() {
            IsActive = false;
        }
    }

    /// <summary>奸奇效果状态同步信道</summary>
    internal sealed class TzeentchEffectNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => TzeentchEffect.HandleNet(reader, whoAmI);
    }
}
