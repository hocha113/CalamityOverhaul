using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class EbnSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => EbnEffect.IsActive || EbnEffect.Sengs > 0f;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(EbnSky.Name, isActive);
    }

    internal sealed class EbnRender : RenderHandle
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static MiscShaderData EbnShader = null!;

        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Noise2 = null!;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!EbnEffect.IsActive && EbnEffect.Sengs <= 0f && !EbnEffect.IsRedScreenActive && !EbnEffect.EpilogueFadeIn) {
                return;
            }

            float baseRadius = 300 + (1f - EbnEffect.Sengs) * 1200;
            if (EbnEffect.IsContracting) {
                baseRadius *= 1f - EbnEffect.ContractionProgress * 0.95f;
            }

            if (EbnEffect.IsActive || EbnEffect.Sengs > 0f) {
                Effect shader = EbnShader.Shader;
                shader.Parameters["colorMult"].SetValue(7.35f);
                shader.Parameters["time"].SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["radius"].SetValue(baseRadius);
                shader.Parameters["setPoint"].SetValue(Main.LocalPlayer.Center);
                shader.Parameters["screenPosition"].SetValue(Main.screenPosition);
                shader.Parameters["screenSize"].SetValue(Main.ScreenSize.ToVector2());
                shader.Parameters["burnIntensity"].SetValue(1f);
                shader.Parameters["maxOpacity"].SetValue(1f);

                spriteBatch.GraphicsDevice.Textures[1] = Noise2;
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
                Rectangle rect = new(Main.screenWidth / 2, Main.screenHeight / 2, Main.screenWidth, Main.screenHeight);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, rect, null, default, 0f, VaultAsset.placeholder2.Value.Size() * 0.5f, 0, 0f);
                spriteBatch.End();
            }

            if (EbnEffect.IsRedScreenActive || EbnEffect.FinalFadeOut || EbnEffect.EpilogueFadeIn) {
                float redAlpha = EbnEffect.RedScreenProgress;
                if (EbnEffect.FinalFadeOut) {
                    redAlpha *= 1f - EbnEffect.GetFadeOutProgress();
                }
                else if (EbnEffect.EpilogueFadeIn) {
                    redAlpha = 1f - EbnEffect.EpilogueFadeProgress;
                }

                if (redAlpha > 0.01f) {
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(180, 20, 10) * redAlpha * 0.95f);
                    spriteBatch.End();
                }
            }
        }
    }

    internal sealed class EbnSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:NarrativeEbnSky";

        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }

            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.03f, 0.05f)
                .UseOpacity(0.75f), EffectPriority.VeryHigh);
        }

        void ICWRLoader.UnLoadData() { }

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

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(25, 3, 2) * intensity * 0.98f);
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 0.4f + 0.6f;
            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(45, 12, 5) * (intensity * 0.25f * pulse));
            float flicker = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 7f) * 0.15f + 0.85f;
            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(35, 5, 3) * (intensity * 0.15f * flicker));
        }

        public override bool IsActive() => active || intensity > 0f;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = EbnEffect.Cek();
            if (EbnEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.025f;
                }
            }
            else {
                intensity -= 0.015f;
                if (intensity <= 0f) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            if (intensity > 0.1f) {
                float effectIntensity = intensity;
                float currentTime = EbnEffect.CekTimer / 60f;
                if (currentTime > 290f) {
                    effectIntensity *= 1f - (currentTime - 290f) / 10f;
                }

                Color tintedColor = new(
                    (int)(inColor.R * 0.75f),
                    (int)(inColor.G * 0.22f),
                    (int)(inColor.B * 0.28f),
                    inColor.A);

                return Color.Lerp(inColor, tintedColor, effectIntensity * 0.75f);
            }

            return inColor;
        }
    }

    internal sealed class EbnEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer;
        public static float Sengs;
        public static bool IsContracting;
        public static float ContractionProgress;
        public static bool IsRedScreenActive;
        public static float RedScreenProgress;
        public static bool FinalFadeOut;
        public static bool EpilogueFadeIn;
        public static float EpilogueFadeProgress;
        public static bool EpilogueComplete;

        private static int contractionTimer;
        private static int redScreenTimer;
        private static int fadeOutTimer;
        private static int epilogueFadeTimer;
        private static float origMusicVolume = -1f;
        private int particleTimer;

        private const int ContractionDuration = 180;
        private const int RedScreenDuration = 120;
        private const int FadeOutDuration = 240;
        private const int EpilogueFadeDuration = 180;

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

        public static float GetFadeOutProgress() => System.Math.Min(1f, fadeOutTimer / (float)FadeOutDuration);

        public static void StartContraction() {
            IsContracting = true;
            ContractionProgress = 0f;
            contractionTimer = 0;
        }

        public static void StartRedScreen() {
            IsRedScreenActive = true;
            RedScreenProgress = 0f;
            redScreenTimer = 0;
        }

        public static void StartEpilogueFadeIn() {
            EpilogueFadeIn = true;
            EpilogueFadeProgress = 0f;
            epilogueFadeTimer = 0;
        }

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
            UpdateState();
            if (!Cek()) {
                return;
            }

            if (++CekTimer > 60 * 60 * 5) {
                IsActive = false;
                return;
            }

            particleTimer++;
            float particleMultiplier = IsContracting ? 1f - ContractionProgress * 0.8f : 1f;

            if (Main.rand.NextFloat() < particleMultiplier) {
                SpawnIntenseBrimstoneFlames();
            }

            if (Main.rand.NextFloat() < particleMultiplier) {
                SpawnAshAndEmbers();
            }

            if (particleTimer % 20 == 0 && Main.rand.NextFloat() < particleMultiplier) {
                SpawnMassiveFlameBurst();
            }

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == ModContent.ProjectileType<ClonePlayer>()) {
                    for (int i = 0; i < 8; i++) {
                        int dust = Dust.NewDust(p.position, p.width, p.height, DustID.RedTorch, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 150, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            CloneFish.Deactivate(Main.LocalPlayer);
            if (Main.musicVolume < 0.6f) {
                origMusicVolume = Main.musicVolume;
                Main.musicVolume = 0.6f;
            }

            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SinsWedge");
        }

        private static void UpdateState() {
            if (IsActive) {
                if (Sengs < 1f) {
                    Sengs += 0.02f;
                }
            }
            else if (Sengs > 0f) {
                if (origMusicVolume > 0f) {
                    Main.musicVolume = origMusicVolume;
                    origMusicVolume = -1f;
                }
                Sengs -= 0.02f;
            }

            if (IsContracting) {
                contractionTimer++;
                ContractionProgress = System.Math.Min(1f, contractionTimer / (float)ContractionDuration);
            }

            if (IsRedScreenActive) {
                redScreenTimer++;
                RedScreenProgress = System.Math.Min(1f, redScreenTimer / (float)RedScreenDuration);
            }

            if (FinalFadeOut && ++fadeOutTimer >= FadeOutDuration) {
                IsActive = false;
                FinalFadeOut = false;
                EpilogueComplete = true;
            }

            if (EpilogueFadeIn) {
                epilogueFadeTimer++;
                EpilogueFadeProgress = System.Math.Min(1f, epilogueFadeTimer / (float)EpilogueFadeDuration);
                if (EpilogueFadeProgress >= 1f) {
                    EpilogueFadeIn = false;
                }
            }

            if (EpilogueComplete) {
                ResetEffects();
            }
        }

        private static void SpawnIntenseBrimstoneFlames() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 4; i++) {
                Vector2 spawnPos = new(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-80, 50));

                var flamePRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-5f, -2.5f)), Color.White, Main.rand.NextFloat(1.2f, 2f));
                if (flamePRT != null) {
                    flamePRT.colors = [new Color(255, 180, 100), new Color(255, 100, 50), new Color(200, 50, 30)];
                    flamePRT.SetLifetime(100, 180);
                }
            }
        }

        private static void SpawnAshAndEmbers() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 5; i++) {
                Vector2 spawnPos = new(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30));

                var ashPRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3.5f, -1.2f)), Color.White, Main.rand.NextFloat(0.7f, 1.3f));
                if (ashPRT != null) {
                    ashPRT.colors = [new Color(90, 80, 70), new Color(60, 50, 45), new Color(30, 25, 20)];
                    ashPRT.SetLifetime(120, 200);
                }
            }

            for (int i = 0; i < 3; i++) {
                Vector2 sparkPos = new(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-40, 20));

                PRTLoader.NewParticle<PRT_Spark>(sparkPos, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -3f)), Color.Lerp(new Color(255, 220, 120), new Color(255, 120, 60), Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f)).Configure(false, Main.rand.Next(30, 70));
            }
        }

        private static void SpawnMassiveFlameBurst() {
            if (Main.dedServ) {
                return;
            }

            Vector2 burstCenter = new(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20));

            const int flameCount = 12;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.4f, 0.4f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);

                var burstFlame = PRTLoader.NewParticle<PRT_LavaFire>(burstCenter + offset, new Vector2(offset.X * 0.08f, Main.rand.NextFloat(-5f, -3f)), Color.White, Main.rand.NextFloat(1.5f, 2.5f));
                if (burstFlame != null) {
                    burstFlame.colors = [new Color(255, 200, 110), new Color(255, 140, 70), new Color(200, 80, 50)];
                    burstFlame.SetLifetime(90, 150);
                }
            }

            for (int i = 0; i < 20; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    burstCenter + Main.rand.NextVector2Circular(150f, 150f),
                    new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-7f, -4f)),
                    Color.Lerp(new Color(255, 220, 100), new Color(255, 100, 50), Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.5f, 2.5f)).Configure(false, Main.rand.Next(35, 75));
            }
        }

        public override void OnWorldLoad() {
            IsActive = false;
            CekTimer = 0;
            Sengs = 0f;
            particleTimer = 0;
            ResetEffects();
        }

        public override void Unload() {
            IsActive = false;
            ResetEffects();
        }
    }
}
