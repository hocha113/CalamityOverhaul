using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    internal sealed class SupCalSkySceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => SupCalEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SupCalSky.Name, isActive);
    }

    internal sealed class SupCalSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:NarrativeSupCalSky";

        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }

            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.1f, 0.05f, 0.08f)
                .UseOpacity(0.6f), EffectPriority.High);
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

            GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height),
                new Color(10, 5, 8) * intensity * 0.9f);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = SupCalEffect.Cek();

            if (SupCalEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.015f;
                }
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            if (intensity > 0.1f) {
                Color tintedColor = new(
                    (int)(inColor.R * 0.8f),
                    (int)(inColor.G * 0.4f),
                    (int)(inColor.B * 0.5f),
                    inColor.A);

                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }

            return inColor;
        }
    }

    internal sealed class SupCalEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer;
        private int particleTimer;

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {
                IsActive = false;
                FirstMetSupCalNPC.Spawned = false;
                FirstMetSupCalNPC.RandomTimer = 0;
                SupCalDefeatNPC.Spawned = false;
                SupCalDefeatNPC.RandomTimer = 0;
                return false;
            }

            return true;
        }

        public override void PostUpdateEverything() {
            if (!Cek()) {
                return;
            }

            if (++CekTimer > 60 * 60 * 3) {
                IsActive = false;
                return;
            }

            particleTimer++;

            SpawnBrimstoneFlameParticles();

            if (particleTimer % 2 == 0) {
                SpawnBrimstoneAshParticles();
            }

            if (particleTimer % 30 == 0) {
                SpawnLargeFlameBurst();
            }

            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Crisis");
        }

        private static void SpawnBrimstoneFlameParticles() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 2; i++) {
                Vector2 spawnPos = new(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30));

                var flamePRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-3.5f, -1.5f)), Color.White, Main.rand.NextFloat(0.8f, 1.4f));

                if (flamePRT != null) {
                    flamePRT.colors = [new Color(255, 140, 70), new Color(200, 80, 40), new Color(140, 40, 30)];
                    flamePRT.SetLifetime(120, 200);
                }
            }
        }

        private static void SpawnBrimstoneAshParticles() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 3; i++) {
                Vector2 spawnPos = new(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20));

                var ashPRT = PRTLoader.NewParticle<PRT_LavaFire>(spawnPos, new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-2.5f, -0.8f)), Color.White, Main.rand.NextFloat(0.5f, 1f));

                if (ashPRT != null) {
                    ashPRT.colors = [new Color(80, 70, 65), new Color(50, 45, 40), new Color(30, 25, 20)];
                    ashPRT.SetLifetime(140, 220);
                }
            }
        }

        private static void SpawnLargeFlameBurst() {
            if (Main.dedServ) {
                return;
            }

            Vector2 burstCenter = new(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.3f, 0.7f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-20, 10));

            const int flameCount = 8;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(20f, 40f);

                var burstFlame = PRTLoader.NewParticle<PRT_LavaFire>(burstCenter + offset, new Vector2(
                    offset.X * 0.05f,
                    Main.rand.NextFloat(-4f, -2f)), Color.White, Main.rand.NextFloat(1.2f, 1.8f));

                if (burstFlame != null) {
                    burstFlame.colors = [new Color(255, 180, 90), new Color(255, 120, 60), new Color(180, 60, 40)];
                    burstFlame.SetLifetime(100, 160);
                }
            }

            for (int i = 0; i < 12; i++) {
                Vector2 sparkVelocity = new(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-5f, -3f));

                PRTLoader.NewParticle<PRT_Spark>(
                    burstCenter + Main.rand.NextVector2Circular(1130f, 130f),
                    sparkVelocity,
                    Color.Lerp(new Color(255, 200, 100), new Color(255, 140, 70), Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.8f)).Configure(false, Main.rand.Next(40, 80));
            }
        }

        public override void OnWorldLoad() {
            IsActive = false;
            CekTimer = 0;
            particleTimer = 0;
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
