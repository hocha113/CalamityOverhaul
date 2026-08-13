using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering
{
    /// <summary>风暴海域 SceneEffect：公爵在场即生效</summary>
    internal class FishronStormSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => FishronStormSky.StormPresent;
        public override void SpecialVisuals(Player player, bool isActive)
            => player.ManageSpecialBiomeVisuals(FishronStormSky.Name, isActive);
    }

    /// <summary>
    /// 风暴天空：越打天越黑。等级由 Boss AI 各端本地上报（无网络包），
    /// 雨幕/雷闪走 <c>FishronStormSky.fx</c>，缺着色器纯色兜底
    /// </summary>
    internal class FishronStormSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:FishronStormSky";

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private bool active;
        private float fadeIn;

        //Boss 每帧上报（各端本地观察，不走包）
        private static int lastReportFrame = -1000;
        private static float targetGrade;
        /// <summary>雷闪冲量与横位（屏幕 0~1）</summary>
        private static float flash;
        private static float flashX = 0.5f;
        //雨量增益冲量与骤停计时
        private static float rainBoost;
        private static int rainCutFrames;

        //平滑后的风暴等级
        private static float grade;

        //远雷氛围计时（纯本地装饰）
        private int ambientThunderTimer;

        /// <summary>上报是否新鲜（Boss 活跃中）</summary>
        internal static bool StormPresent => Main.GameUpdateCount - (uint)lastReportFrame < 12u;

        #region 上报与冲量 API
        /// <summary>Boss AI 每帧上报风暴等级（各端本地）</summary>
        public static void Report(NPC npc, float stormGrade) {
            if (npc == null || !npc.active) {
                return;
            }
            lastReportFrame = (int)Main.GameUpdateCount;
            targetGrade = MathHelper.Clamp(stormGrade, 0f, 1f);
        }

        /// <summary>天光雷闪（客户端表现）</summary>
        public static void PushFlash(float strength, Vector2 worldPos) {
            if (VaultUtils.isServer) {
                return;
            }
            strength = MathHelper.Clamp(strength, 0f, 1f);
            if (strength < flash) {
                return;
            }
            flash = strength;
            flashX = MathHelper.Clamp((worldPos.X - Main.screenPosition.X) / Main.screenWidth, 0.08f, 0.92f);
        }

        /// <summary>临时抬升雨量（取同帧最大）</summary>
        public static void PushRainBoost(float amount) {
            if (VaultUtils.isServer) {
                return;
            }
            rainBoost = Math.Max(rainBoost, MathHelper.Clamp(amount, 0f, 1f));
        }

        /// <summary>雨声骤停：大招死寂拍</summary>
        public static void PushRainCut(int frames = 4) {
            if (VaultUtils.isServer) {
                return;
            }
            rainCutFrames = Math.Max(rainCutFrames, frames);
        }

        /// <summary>卸载/清理</summary>
        public static void Clear() {
            lastReportFrame = -1000;
            targetGrade = 0f;
            flash = 0f;
            rainBoost = 0f;
            rainCutFrames = 0;
            grade = 0f;
        }
        #endregion

        /// <summary>当前雨量（含增益与骤停）</summary>
        private static float CurrentRain {
            get {
                if (rainCutFrames > 0) {
                    return 0f;
                }
                float baseRain = MathHelper.Clamp(grade * 1.25f - 0.28f, 0f, 1f);
                return MathHelper.Clamp(baseRain + rainBoost, 0f, 1f);
            }
        }

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.05f, 0.1f, 0.12f)
                .UseOpacity(0.3f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            fadeIn = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() => active || fadeIn > 0f;

        public override void Reset() {
            active = false;
            fadeIn = 0f;
        }

        public override void Update(GameTime gameTime) {
            if (StormPresent) {
                fadeIn = Math.Min(fadeIn + 0.02f, 1f);
            }
            else {
                fadeIn -= 0.012f;
                if (fadeIn <= 0f) {
                    fadeIn = 0f;
                    Deactivate();
                }
            }

            //等级平滑追踪上报值
            grade = MathHelper.Lerp(grade, StormPresent ? targetGrade : 0f, 0.03f);

            //冲量衰减
            flash *= 0.88f;
            if (flash < 0.01f) {
                flash = 0f;
            }
            rainBoost *= 0.9f;
            if (rainBoost < 0.01f) {
                rainBoost = 0f;
            }
            if (rainCutFrames > 0) {
                rainCutFrames--;
            }

            //滤镜浓度随风暴推进
            float eff = fadeIn * grade;
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.15f + eff * 0.4f);

            //远雷氛围：三阶段的黑夜里雷声不散
            if (eff > 0.65f && !Main.gamePaused) {
                ambientThunderTimer--;
                if (ambientThunderTimer <= 0) {
                    ambientThunderTimer = Main.rand.Next(240, 520);
                    PushFlash(Main.rand.NextFloat(0.15f, 0.3f),
                        Main.screenPosition + new Vector2(Main.rand.NextFloat() * Main.screenWidth, 0f));
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Volume = 0.35f,
                        Pitch = -0.7f,
                        MaxInstances = 2
                    });
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (fadeIn <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            //只画最底层
            if (maxDepth < 0 || minDepth >= 0) {
                return;
            }

            Effect shader = EffectLoader.FishronStormSky?.Value;
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            if (shader == null || noiseTex == null) {
                //纯色兜底：压暗+闪白
                Color dark = new Color(8, 16, 22) * (fadeIn * grade * 0.85f);
                spriteBatch.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), dark);
                if (flash > 0.02f) {
                    spriteBatch.Draw(VaultAsset.placeholder2.Value,
                        new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                        new Color(200, 230, 255) * (flash * 0.35f));
                }
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uIntensity"]?.SetValue(fadeIn * grade);
            shader.Parameters["uRain"]?.SetValue(CurrentRain * fadeIn);
            shader.Parameters["uFlash"]?.SetValue(flash);
            shader.Parameters["uFlashX"]?.SetValue(flashX);
            shader.Parameters["uAspectRatio"]?.SetValue(vpW / (float)vpH);
            shader.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }

        public override Color OnTileColor(Color inColor) {
            float eff = fadeIn * grade;
            if (eff <= 0.05f) {
                return inColor;
            }
            //风暴压暗世界，闪电瞬间反向提亮
            Color darkened = new(
                (int)(inColor.R * (1f - eff * 0.45f)),
                (int)(inColor.G * (1f - eff * 0.38f)),
                (int)(inColor.B * (1f - eff * 0.22f)),
                inColor.A);
            if (flash > 0.02f) {
                darkened = Color.Lerp(darkened, new Color(210, 235, 255), flash * 0.5f);
            }
            return darkened;
        }
    }
}
