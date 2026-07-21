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

    /// <summary>奸奇效果开关与同步</summary>
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

            if (++CekTimer > 60 * 60 * 3) //最多持续3分钟
            {
                IsActive = false;
                return;
            }

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
