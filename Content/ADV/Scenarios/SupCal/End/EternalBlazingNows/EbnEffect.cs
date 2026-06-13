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
    /// <summary>Ebn 场景 ModSceneEffect——音乐由 EbnSkyEffect 控</summary>
    internal class EbnSceneEffect : ModSceneEffect
    {
        public override int Music => -1;//音乐在 EbnSkyEffect
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => EbnEffect.IsActive || EbnEffect.Sengs > 0f;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(EbnSky.Name, isActive);
    }

    /// <summary>Ebn 场景 RenderHandle——EndCapture 火圈着色器+红屏叠层</summary>
    internal class EbnRender : RenderHandle
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static MiscShaderData EbnShader = null!;
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Noise2 = null!;
        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!EbnEffect.IsActive && EbnEffect.Sengs <= 0 && !EbnEffect.IsRedScreenActive && !EbnEffect.EpilogueFadeIn) {
                return;
            }

            var maxOpacity = 1f;

            //火圈半径，收缩时快速缩小
            float baseRadius = 300 + (1f - EbnEffect.Sengs) * 1200;
            if (EbnEffect.IsContracting) {
                baseRadius *= (1f - EbnEffect.ContractionProgress * 0.95f);//缩至约 5%
            }

            //火圈 pass
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

            //红屏叠层
            if (EbnEffect.IsRedScreenActive || EbnEffect.FinalFadeOut || EbnEffect.EpilogueFadeIn) {
                float redAlpha = EbnEffect.RedScreenProgress;

                if (EbnEffect.FinalFadeOut) {
                    //最终淡出
                    float fadeProgress = EbnEffect.GetFadeOutProgress();
                    redAlpha *= (1f - fadeProgress);
                }
                else if (EbnEffect.EpilogueFadeIn) {
                    //尾声淡入，红屏继续淡出
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

    /// <summary>Ebn 天空 CustomSky——暗红硫磺火叠层+物块色调</summary>
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
            //暗红 Scene 滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.03f, 0.05f)//深红暗调
                .UseOpacity(0.75f), EffectPriority.VeryHigh);
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

            //暗红底
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(25, 3, 2) * skyIntensity * 0.98f
            );

            //脉动光晕
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 0.4f + 0.6f;
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(45, 12, 5) * (skyIntensity * 0.25f * pulse)
            );

            //红色闪烁层
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

            //按 EbnEffect 状态淡入/淡出 intensity
            if (EbnEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.025f;
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
            //暗红硫磺火物块色调，尾声前 fadeOut
            if (intensity > 0.1f) {
                float currentTime = EbnEffect.CekTimer / 60f;
                float maxTime = 300f;
                float fadeOutTime = 10f;

                float effectIntensity = intensity;
                if (currentTime > maxTime - fadeOutTime) {
                    float fadeProgress = (currentTime - (maxTime - fadeOutTime)) / fadeOutTime;
                    effectIntensity *= (1f - fadeProgress);
                }

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

    /// <summary>Ebn 场景 ModSystem——火圈/红屏/粒子/音乐状态机</summary>
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
        private const int ContractionDuration = 180;//3s

        //红屏效果相关
        public static bool IsRedScreenActive = false;
        public static float RedScreenProgress = 0f;
        private static int redScreenTimer = 0;
        private const int RedScreenDuration = 120;//2s 过渡到全红

        //最终淡出
        public static bool FinalFadeOut = false;
        private static int fadeOutTimer = 0;
        private const int FadeOutDuration = 240;//4s 全淡出

        //尾声淡入相关
        public static bool EpilogueFadeIn = false;
        public static float EpilogueFadeProgress = 0f;
        private static int epilogueFadeTimer = 0;
        private const int EpilogueFadeDuration = 180;//3s 尾声淡入
        public static bool EpilogueComplete = false;

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {
                //主菜单自动关
                IsActive = false;
                return false;
            }

            return true;
        }

        /// <summary>淡出进度 0~1</summary>
        public static float GetFadeOutProgress() {
            return Math.Min(1f, fadeOutTimer / (float)FadeOutDuration);
        }

        /// <summary>启动火圈收缩</summary>
        public static void StartContraction() {
            IsContracting = true;
            ContractionProgress = 0f;
            contractionTimer = 0;
        }

        /// <summary>启动红屏</summary>
        public static void StartRedScreen() {
            IsRedScreenActive = true;
            RedScreenProgress = 0f;
            redScreenTimer = 0;
        }

        /// <summary>启动尾声淡入</summary>
        public static void StartEpilogueFadeIn() {
            EpilogueFadeIn = true;
            EpilogueFadeProgress = 0f;
            epilogueFadeTimer = 0;
        }

        /// <summary>重置火圈/红屏/淡出/尾声状态</summary>
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

                //收缩满后红屏由对话触发，非自动
                if (ContractionProgress >= 1f && !IsRedScreenActive) {
                    //StartRedScreen();
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

                //淡出完成关场景
                if (fadeProgress >= 1f) {
                    IsActive = false;
                    FinalFadeOut = false;
                    EpilogueComplete = true;
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

            if (++CekTimer > 60 * 60 * 5)//最长 5 分钟
            {
                IsActive = false;
                return;
            }

            particleTimer++;

            //收缩时粒子衰减
            float particleMultiplier = IsContracting ? (1f - ContractionProgress * 0.8f) : 1f;
            if (particleTimer % 1 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnIntenseBrimstoneFlames();
            }

            if (particleTimer % 1 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnAshAndEmbers();
            }

            if (particleTimer % 20 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnMassiveFlameBurst();
            }

            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == ModContent.ProjectileType<ClonePlayer>()) {
                    //ClonePlayer 火焰 dust——封锁过去视觉
                    for (int i = 0; i < 8; i++) {
                        int dust = Dust.NewDust(p.position, p.width, p.height, DustID.RedTorch, Main.rand.NextFloat(-2f, 2f)
                            , Main.rand.NextFloat(-2f, 2f), 150, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            CloneFish.Deactivate(Main.LocalPlayer);//强制关闭克隆鱼

            if (Main.musicVolume < 0.6f) {
                origMusicVolume = Main.musicVolume;
                Main.musicVolume = 0.6f;
            }
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SinsWedge");
        }

        private static float origMusicVolume = -1;

        /// <summary>屏幕边缘硫磺火焰 PRT</summary>
        private static void SpawnIntenseBrimstoneFlames() {
            //屏幕下方随机 spawn
            for (int i = 0; i < 4; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-80, 50)
                );

                var flamePRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(
                    Main.rand.NextFloat(-2.5f, 2.5f),
                    Main.rand.NextFloat(-5f, -2.5f)), Color.White, Main.rand.NextFloat(1.2f, 2f));
                if (flamePRT != null) {
                    flamePRT.colors = [new Color(255, 180, 100), new Color(255, 100, 50), new Color(200, 50, 30)];
                    flamePRT.SetLifetime(100, 180);
                }
            }
        }

        /// <summary>灰烬+火星 PRT</summary>
        private static void SpawnAshAndEmbers() {
            //生成密集的灰烬
            for (int i = 0; i < 5; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30)
                );

                var ashPRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(
                    Main.rand.NextFloat(-3f, 3f),
                    Main.rand.NextFloat(-3.5f, -1.2f)), Color.White, Main.rand.NextFloat(0.7f, 1.3f));
                if (ashPRT != null) {
                    ashPRT.colors = [new Color(90, 80, 70), new Color(60, 50, 45), new Color(30, 25, 20)];
                    ashPRT.SetLifetime(120, 200);
                }
            }

            //生成火星
            for (int i = 0; i < 3; i++) {
                Vector2 sparkPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-40, 20)
                );

                PRTLoader.NewParticle<PRT_Spark>(sparkPos, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -3f)), Color.Lerp(new Color(255, 220, 120), new Color(255, 120, 60), Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f)).Configure(false, Main.rand.Next(30, 70));
            }
        }

        /// <summary>环形火焰+火星爆发 PRT</summary>
        private static void SpawnMassiveFlameBurst() {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20)
            );

            //环形火焰
            int flameCount = 12;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.4f, 0.4f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);

                var burstFlame = PRTLoader.NewParticle<PRT_LavaFire>(burstCenter + offset, new Vector2(
                    offset.X * 0.08f,
                    Main.rand.NextFloat(-5f, -3f)), Color.White, Main.rand.NextFloat(1.5f, 2.5f));
                if (burstFlame != null) {
                    burstFlame.colors = [new Color(255, 200, 110), new Color(255, 140, 70), new Color(200, 80, 50)];
                    burstFlame.SetLifetime(90, 150);
                }
            }

            //额外火星
            for (int i = 0; i < 20; i++) {
                Vector2 sparkVelocity = new Vector2(
                    Main.rand.NextFloat(-4f, 4f),
                    Main.rand.NextFloat(-7f, -4f)
                );

                PRTLoader.NewParticle<PRT_Spark>(burstCenter + Main.rand.NextVector2Circular(150f, 150f), sparkVelocity, Color.Lerp(new Color(255, 220, 100), new Color(255, 100, 50), Main.rand.NextFloat()), Main.rand.NextFloat(1.5f, 2.5f)).Configure(false, Main.rand.Next(35, 75));
            }
        }

        public override void OnWorldLoad() {
            IsActive = false;
            CekTimer = 0;
            Sengs = 0f;
            particleTimer = 0;
            ResetEffects();
        }

        public override void PostSetupContent() {
            ADVScenarioScheduler.RegisterBlocker(() =>
                IsActive ? ScenarioBlockers.Cutscene : ScenarioBlockers.None);
        }

        public override void Unload() {
            IsActive = false;
            ResetEffects();
        }
    }
}
