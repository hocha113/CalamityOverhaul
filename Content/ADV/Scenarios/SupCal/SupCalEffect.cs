using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class SupCalSkySceneEffect : ModSceneEffect
    {
        public override int Music => -1;//音乐在 SupCalSkyPlayer 里控制
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => SupCalSkyEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SupCalSky.Name, isActive);
    }

    ///<summary>
    ///至尊灾厄天空效果
    ///</summary>
    internal class SupCalSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:SupCalSky";
        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;
            //创建暗黑滤镜效果
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.1f, 0.05f, 0.08f)  //深红暗色调
                .UseOpacity(0.6f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) return;

            //绘制深红暗黑背景
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(10, 5, 8) * intensity * 0.9f
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
            _ = SupCalSkyEffect.Cek();
            //根据对话场景状态调整强度
            if (SupCalSkyEffect.IsActive) {
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
            //应用暗红色调
            if (intensity > 0.1f) {
                float darkR = 0.8f;
                float darkG = 0.4f;
                float darkB = 0.5f;

                Color tintedColor = new Color(
                    (int)(inColor.R * darkR),
                    (int)(inColor.G * darkG),
                    (int)(inColor.B * darkB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }
            return inColor;
        }
    }

    ///<summary>
    ///至尊灾厄场景效果管理器（负责粒子生成）
    ///</summary>
    internal class SupCalSkyEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;

        public static bool Cek() {
            if (!IsActive) {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu) {//主菜单界面自动关闭效果
                IsActive = false;
                //如果退出到主菜单，重置对话场景状态
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

            if (++CekTimer > 60 * 60 * 3) {//一次开启后最多持续3分钟
                IsActive = false;
                return;
            }

            particleTimer++;

            //更频繁地生成火焰粒子
            if (particleTimer % 1 == 0) {
                SpawnBrimstoneFlameParticles();
            }

            //生成灰烬粒子
            if (particleTimer % 2 == 0) {
                SpawnBrimstoneAshParticles();
            }

            //偶尔生成大型火焰团
            if (particleTimer % 30 == 0) {
                SpawnLargeFlameBurst();
            }

            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Crisis");
        }

        private static void SpawnBrimstoneFlameParticles() {
            //在屏幕下半部分随机位置生成多个火焰粒子
            for (int i = 0; i < 2; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30)
                );

                //创建硫磺火粒子
                PRT_LavaFire flamePRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-1.5f, 1.5f),
                        Main.rand.NextFloat(-3.5f, -1.5f)  //更强的上升力
                    ),
                    Scale = Main.rand.NextFloat(0.8f, 1.4f),
                    ai = new float[] { 0, 0 },  //ai[1] = 0 表示使用标准漂浮模式
                    colors = new Color[] {
                        new Color(255, 140, 70),   //亮橙色
                        new Color(200, 80, 40),    //暗橙红
                        new Color(140, 40, 30)     //深红
                    },
                    minLifeTime = 120,
                    maxLifeTime = 200
                };

                PRTLoader.AddParticle(flamePRT);
            }
        }

        private static void SpawnBrimstoneAshParticles() {
            //生成灰烬粒子，覆盖更大范围
            for (int i = 0; i < 3; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20)
                );

                //使用 LavaFire 的变体作为灰烬
                PRT_LavaFire ashPRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-2f, 2f),
                        Main.rand.NextFloat(-2.5f, -0.8f)
                    ),
                    Scale = Main.rand.NextFloat(0.5f, 1f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(80, 70, 65),     //灰褐色
                        new Color(50, 45, 40),     //深灰
                        new Color(30, 25, 20)      //暗灰黑
                    },
                    minLifeTime = 140,
                    maxLifeTime = 220
                };

                PRTLoader.AddParticle(ashPRT);
            }
        }

        private static void SpawnLargeFlameBurst() {
            //在屏幕底部中间区域生成大型火焰爆发
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.3f, 0.7f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-20, 10)
            );

            //生成一组环绕的火焰粒子
            int flameCount = 8;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(20f, 40f);

                PRT_LavaFire burstFlame = new PRT_LavaFire {
                    Position = burstCenter + offset,
                    Velocity = new Vector2(
                        offset.X * 0.05f,
                        Main.rand.NextFloat(-4f, -2f)
                    ),
                    Scale = Main.rand.NextFloat(1.2f, 1.8f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(255, 180, 90),   //非常亮的橙黄
                        new Color(255, 120, 60),   //亮橙
                        new Color(180, 60, 40)     //深橙红
                    },
                    minLifeTime = 100,
                    maxLifeTime = 160
                };

                PRTLoader.AddParticle(burstFlame);
            }

            //额外生成一些快速上升的火星
            for (int i = 0; i < 12; i++) {
                Vector2 sparkVelocity = new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-5f, -3f)
                );

                PRT_Spark spark = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(1130f, 130f),
                    sparkVelocity,
                    false,
                    Main.rand.Next(40, 80),
                    Main.rand.NextFloat(1f, 1.8f),
                    Color.Lerp(
                        new Color(255, 200, 100),
                        new Color(255, 140, 70),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
