using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 永恒燃烧的如今天空场景效果
    /// </summary>
    internal class EbnSceneEffect : ModSceneEffect
    {
        public override int Music => -1; // 音乐在 EbnSkyEffect 里控制
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => EbnEffect.IsActive || EbnEffect.Sengs > 0f;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(EbnSky.Name, isActive);
    }

    internal class EbnRender : RenderHandle//渲染控制
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static MiscShaderData EbnShader;
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Noise2;
        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!EbnEffect.IsActive && EbnEffect.Sengs <= 0 && !EbnEffect.IsRedScreenActive && !EbnEffect.EpilogueFadeIn) {
                return;
            }

            var maxOpacity = 1f;

            //计算火圈半径 - 考虑收缩效果
            float baseRadius = 300 + (1f - EbnEffect.Sengs) * 1200;
            if (EbnEffect.IsContracting) {
                //收缩时半径快速减小
                baseRadius *= (1f - EbnEffect.ContractionProgress * 0.95f); // 收缩到原来的5%
            }

            //只在效果激活时绘制火圈
            if (EbnEffect.IsActive || EbnEffect.Sengs > 0) {
                var shader = EbnShader.Shader;
                shader.Parameters["colorMult"].SetValue(7.35f);
                shader.Parameters["time"].SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["radius"].SetValue(baseRadius);
                shader.Parameters["setPoint"].SetValue(Main.LocalPlayer.Center);
                shader.Parameters["screenPosition"].SetValue(Main.screenPosition);
                shader.Parameters["screenSize"].SetValue(Main.ScreenSize.ToVector2());
                shader.Parameters["burnIntensity"].SetValue(1f);
                shader.Parameters["maxOpacity"].SetValue(maxOpacity);

                spriteBatch.GraphicsDevice.Textures[1] = Noise2;

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
                Rectangle rekt = new(Main.screenWidth / 2, Main.screenHeight / 2, Main.screenWidth, Main.screenHeight);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, rekt, null, default, 0f, VaultAsset.placeholder2.Value.Size() * 0.5f, 0, 0f);
                spriteBatch.End();
            }

            //绘制红屏效果
            if (EbnEffect.IsRedScreenActive || EbnEffect.FinalFadeOut || EbnEffect.EpilogueFadeIn) {
                float redAlpha = EbnEffect.RedScreenProgress;

                if (EbnEffect.FinalFadeOut) {
                    //最终淡出时逐渐减少红屏，使用GetFadeOutProgress获取进度
                    float fadeProgress = EbnEffect.GetFadeOutProgress();
                    redAlpha *= (1f - fadeProgress);
                }
                else if (EbnEffect.EpilogueFadeIn) {
                    //尾声淡入时，红屏继续淡出
                    redAlpha = 1f - EbnEffect.EpilogueFadeProgress;
                }

                if (redAlpha > 0.01f) {
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    spriteBatch.Draw(
                        VaultAsset.placeholder2.Value,
                        new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                        new Color(180, 20, 10) * redAlpha * 0.95f
                    );
                    spriteBatch.End();
                }
            }
        }
    }

    /// <summary>
    /// 永恒燃烧的如今天空效果
    /// 比至尊灾厄的硫磺火效果更加极端和恐怖
    /// </summary>
    internal class EbnSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:EbnSky";
        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;
            // 创建更加强烈的暗红滤镜效果
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.03f, 0.05f)  // 更深的红暗色调
                .UseOpacity(0.75f), EffectPriority.VeryHigh); // 更高的不透明度
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed)
                return;

            float skyIntensity = intensity;

            // 绘制更深的暗红硫磺火背景，带有更强的颗粒感
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(25, 3, 2) * skyIntensity * 0.98f
            );

            // 添加脉动的火焰光晕效果，模拟硫磺燃烧的波动
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 0.4f + 0.6f;
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(45, 12, 5) * (skyIntensity * 0.25f * pulse)
            );

            // 添加额外的红色闪烁层，模拟硫磺火焰的不稳定性
            float flicker = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 7f) * 0.15f + 0.85f;
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(35, 5, 3) * (skyIntensity * 0.15f * flicker)
            );
        }

        public override bool IsActive() {
            return active || intensity > 0;
        }

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = EbnEffect.Cek();

            //根据场景状态调整强度
            if (EbnEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.025f; //稍快的淡入速度
                }
            }
            else {
                intensity -= 0.015f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用更强的暗红硫磺火色调
            if (intensity > 0.1f) {
                //计算淡出效果
                float currentTime = EbnEffect.CekTimer / 60f;
                float maxTime = 300f;
                float fadeOutTime = 10f;

                float effectIntensity = intensity;
                if (currentTime > maxTime - fadeOutTime) {
                    float fadeProgress = (currentTime - (maxTime - fadeOutTime)) / fadeOutTime;
                    effectIntensity *= (1f - fadeProgress);
                }

                //更强的红色调，更弱的其他颜色
                float darkR = 0.75f;
                float darkG = 0.22f;
                float darkB = 0.28f;

                Color tintedColor = new Color(
                    (int)(inColor.R * darkR),
                    (int)(inColor.G * darkG),
                    (int)(inColor.B * darkB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, effectIntensity * 0.75f);
            }
            return inColor;
        }
    }

    /// <summary>
    /// 永恒燃烧的如今场景效果管理器（负责粒子生成）
    /// </summary>
    internal class EbnEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        public static float Sengs;
        private int particleTimer = 0;

        //火圈收缩相关
        public static bool IsContracting = false;
        public static float ContractionProgress = 0f;
        private static int contractionTimer = 0;
        private const int ContractionDuration = 180; // 3秒收缩时间

        //红屏效果相关
        public static bool IsRedScreenActive = false;
        public static float RedScreenProgress = 0f;
        private static int redScreenTimer = 0;
        private const int RedScreenDuration = 120; // 2秒过渡到完全红屏

        //最终淡出
        public static bool FinalFadeOut = false;
        private static int fadeOutTimer = 0;
        private const int FadeOutDuration = 240; // 4秒完全淡出

        //尾声淡入相关
        public static bool EpilogueFadeIn = false;
        public static float EpilogueFadeProgress = 0f;
        private static int epilogueFadeTimer = 0;
        private const int EpilogueFadeDuration = 180; // 3秒淡入
        public static bool EpilogueComplete = false;

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {
                // 主菜单界面自动关闭效果
                IsActive = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取淡出进度
        /// </summary>
        public static float GetFadeOutProgress() {
            return Math.Min(1f, fadeOutTimer / (float)FadeOutDuration);
        }

        /// <summary>
        /// 开始火圈收缩
        /// </summary>
        public static void StartContraction() {
            IsContracting = true;
            ContractionProgress = 0f;
            contractionTimer = 0;
        }

        /// <summary>
        /// 开始红屏效果
        /// </summary>
        public static void StartRedScreen() {
            IsRedScreenActive = true;
            RedScreenProgress = 0f;
            redScreenTimer = 0;
        }

        /// <summary>
        /// 开始尾声淡入效果
        /// </summary>
        public static void StartEpilogueFadeIn() {
            EpilogueFadeIn = true;
            EpilogueFadeProgress = 0f;
            epilogueFadeTimer = 0;
        }

        /// <summary>
        /// 重置所有效果
        /// </summary>
        public static void ResetEffects() {
            IsContracting = false;
            ContractionProgress = 0f;
            contractionTimer = 0;

            IsRedScreenActive = false;
            RedScreenProgress = 0f;
            redScreenTimer = 0;

            FinalFadeOut = false;
            fadeOutTimer = 0;

            EpilogueFadeIn = false;
            EpilogueFadeProgress = 0f;
            epilogueFadeTimer = 0;
            EpilogueComplete = false;
        }

        public override void PostUpdateEverything() {
            if (IsActive) {
                if (Sengs < 1f) {
                    Sengs += 0.02f;
                }
            }
            else {
                if (Sengs > 0f) {
                    if (origMusicVolume > 0f) {
                        Main.musicVolume = origMusicVolume;
                        origMusicVolume = -1f;
                    }
                    Sengs -= 0.02f;
                }
            }

            //处理火圈收缩
            if (IsContracting) {
                contractionTimer++;
                ContractionProgress = Math.Min(1f, contractionTimer / (float)ContractionDuration);

                //收缩完成后自动触发红屏
                if (ContractionProgress >= 1f && !IsRedScreenActive) {
                    //StartRedScreen(); // 由对话触发，不自动触发
                }
            }

            //处理红屏效果
            if (IsRedScreenActive) {
                redScreenTimer++;
                RedScreenProgress = Math.Min(1f, redScreenTimer / (float)RedScreenDuration);
            }

            //处理最终淡出
            if (FinalFadeOut) {
                fadeOutTimer++;
                float fadeProgress = Math.Min(1f, fadeOutTimer / (float)FadeOutDuration);

                //淡出完成后关闭所有效果
                if (fadeProgress >= 1f) {
                    IsActive = false;
                    FinalFadeOut = false;
                    //不立即重置，等待尾声场景
                    return;
                }
            }

            //处理尾声淡入
            if (EpilogueFadeIn) {
                epilogueFadeTimer++;
                EpilogueFadeProgress = Math.Min(1f, epilogueFadeTimer / (float)EpilogueFadeDuration);

                //淡入完成
                if (EpilogueFadeProgress >= 1f) {
                    EpilogueFadeIn = false;
                }
            }

            //尾声完成后完全重置
            if (EpilogueComplete) {
                ResetEffects();
                return;
            }

            if (!Cek()) {
                return;
            }

            if (++CekTimer > 60 * 60 * 5) // 最多持续5分钟
            {
                IsActive = false;
                return;
            }

            particleTimer++;

            //火圈收缩时减少粒子生成
            float particleMultiplier = IsContracting ? (1f - ContractionProgress * 0.8f) : 1f;

            // 生成更密集的火焰粒子
            if (particleTimer % 1 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnIntenseBrimstoneFlames();
            }

            // 生成大量灰烬和火星
            if (particleTimer % 1 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnAshAndEmbers();
            }

            // 频繁生成大型火焰爆发
            if (particleTimer % 20 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnMassiveFlameBurst();
            }

            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == ModContent.ProjectileType<ClonePlayer>()) {
                    //遍历生成火焰粒子，表示被封锁过去
                    for (int i = 0; i < 8; i++) {
                        int dust = Dust.NewDust(p.position, p.width, p.height, DustID.RedTorch, Main.rand.NextFloat(-2f, 2f)
                            , Main.rand.NextFloat(-2f, 2f), 150, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            CloneFish.Deactivate(Main.LocalPlayer);//强行设置消失

            if (Main.musicVolume < 0.6f) {
                origMusicVolume = Main.musicVolume;
                Main.musicVolume = 0.6f;
            }
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SinsWedge");
        }

        private static float origMusicVolume = -1;

        /// <summary>
        /// 生成强烈的硫磺火焰粒子
        /// </summary>
        private static void SpawnIntenseBrimstoneFlames() {
            // 在屏幕各处生成火焰粒子
            for (int i = 0; i < 4; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-80, 50)
                );

                PRT_LavaFire flamePRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-2.5f, 2.5f),
                        Main.rand.NextFloat(-5f, -2.5f)
                    ),
                    Scale = Main.rand.NextFloat(1.2f, 2f),
                    ai = [0, 0],
                    colors = [
                        new Color(255, 180, 100),//极亮的橙黄
                        new Color(255, 100, 50), //明亮的橙红
                        new Color(200, 50, 30),  //深红
                        new Color(100, 20, 10)   //暗红
                    ],
                    minLifeTime = 100,
                    maxLifeTime = 180
                };

                PRTLoader.AddParticle(flamePRT);
            }
        }

        /// <summary>
        /// 生成灰烬和火星粒子
        /// </summary>
        private static void SpawnAshAndEmbers() {
            // 生成密集的灰烬
            for (int i = 0; i < 5; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30)
                );

                PRT_LavaFire ashPRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-3f, 3f),
                        Main.rand.NextFloat(-3.5f, -1.2f)
                    ),
                    Scale = Main.rand.NextFloat(0.7f, 1.3f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(90, 80, 70),
                        new Color(60, 50, 45),
                        new Color(30, 25, 20)
                    },
                    minLifeTime = 120,
                    maxLifeTime = 200
                };

                PRTLoader.AddParticle(ashPRT);
            }

            // 生成火星
            for (int i = 0; i < 3; i++) {
                Vector2 sparkPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-40, 20)
                );

                PRT_Spark spark = new PRT_Spark(
                    sparkPos,
                    new Vector2(
                        Main.rand.NextFloat(-3f, 3f),
                        Main.rand.NextFloat(-6f, -3f)
                    ),
                    false,
                    Main.rand.Next(30, 70),
                    Main.rand.NextFloat(1.2f, 2f),
                    Color.Lerp(
                        new Color(255, 220, 120),
                        new Color(255, 120, 60),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(spark);
            }
        }

        /// <summary>
        /// 生成大型火焰爆发
        /// </summary>
        private static void SpawnMassiveFlameBurst() {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20)
            );

            // 生成环形火焰爆发
            int flameCount = 12;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.4f, 0.4f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);

                PRT_LavaFire burstFlame = new PRT_LavaFire {
                    Position = burstCenter + offset,
                    Velocity = new Vector2(
                        offset.X * 0.08f,
                        Main.rand.NextFloat(-5f, -3f)
                    ),
                    Scale = Main.rand.NextFloat(1.5f, 2.5f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(255, 200, 110),
                        new Color(255, 140, 70),
                        new Color(200, 80, 50),
                        new Color(120, 40, 30)
                    },
                    minLifeTime = 90,
                    maxLifeTime = 150
                };

                PRTLoader.AddParticle(burstFlame);
            }

            // 额外的火星爆发
            for (int i = 0; i < 20; i++) {
                Vector2 sparkVelocity = new Vector2(
                    Main.rand.NextFloat(-4f, 4f),
                    Main.rand.NextFloat(-7f, -4f)
                );

                PRT_Spark spark = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(150f, 150f),
                    sparkVelocity,
                    false,
                    Main.rand.Next(35, 75),
                    Main.rand.NextFloat(1.5f, 2.5f),
                    Color.Lerp(
                        new Color(255, 220, 100),
                        new Color(255, 100, 50),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void Unload() {
            IsActive = false;
            ResetEffects();
        }
    }
}
