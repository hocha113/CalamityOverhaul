using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
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
        public override bool IsSceneEffectActive(Player player) => EbnSkyEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(EbnSky.Name, isActive);
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

        void ICWRLoader.LoadData()
        {
            if (VaultUtils.isServer)
            {
                return;
            }
            SkyManager.Instance[Name] = this;
            // 创建更加强烈的暗红滤镜效果
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.03f, 0.05f)  // 更深的红暗色调
                .UseOpacity(0.75f), EffectPriority.VeryHigh); // 更高的不透明度
        }

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
            intensity = 0f;
        }

        public override void Deactivate(params object[] args)
        {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) return;

            // 绘制更深的暗红背景
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(15, 3, 5) * intensity * 0.95f
            );

            // 添加脉动的火焰光晕效果
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(30, 10, 5) * (intensity * 0.3f * pulse)
            );
        }

        public override bool IsActive()
        {
            return active || intensity > 0;
        }

        public override void Reset()
        {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime)
        {
            _ = EbnSkyEffect.Cek();
            // 根据场景状态调整强度
            if (EbnSkyEffect.IsActive)
            {
                if (intensity < 1f)
                {
                    intensity += 0.02f; // 稍快的淡入速度
                }
            }
            else
            {
                intensity -= 0.012f;
                if (intensity <= 0)
                {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor)
        {
            // 应用更强的暗红色调
            if (intensity > 0.1f)
            {
                float darkR = 0.7f;
                float darkG = 0.3f;
                float darkB = 0.4f;

                Color tintedColor = new Color(
                    (int)(inColor.R * darkR),
                    (int)(inColor.G * darkG),
                    (int)(inColor.B * darkB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.65f);
            }
            return inColor;
        }
    }

    /// <summary>
    /// 永恒燃烧的如今场景效果管理器（负责粒子生成）
    /// </summary>
    internal class EbnSkyEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;

        public static bool Cek()
        {
            if (!IsActive)
            {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu)
            {
                // 主菜单界面自动关闭效果
                IsActive = false;
                EternalBlazingNowNPC.Spawned = false;
                EternalBlazingNowNPC.RandomTimer = 0;
                return false;
            }

            return true;
        }

        public override void PostUpdateEverything()
        {
            if (!Cek())
            {
                return;
            }

            if (++CekTimer > 60 * 60 * 5) // 最多持续5分钟
            {
                IsActive = false;
                return;
            }

            particleTimer++;

            // 生成更密集的火焰粒子
            if (particleTimer % 1 == 0)
            {
                SpawnIntenseBrimstoneFlames();
            }

            // 生成大量灰烬和火星
            if (particleTimer % 1 == 0)
            {
                SpawnAshAndEmbers();
            }

            // 频繁生成大型火焰爆发
            if (particleTimer % 20 == 0)
            {
                SpawnMassiveFlameBurst();
            }

            // 偶尔生成火焰漩涡
            if (particleTimer % 60 == 0)
            {
                SpawnFlameVortex();
            }

            // 播放危机音乐
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Crisis");
        }

        /// <summary>
        /// 生成强烈的硫磺火焰粒子
        /// </summary>
        private static void SpawnIntenseBrimstoneFlames()
        {
            // 在屏幕各处生成火焰粒子
            for (int i = 0; i < 4; i++)
            {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-80, 50)
                );

                PRT_LavaFire flamePRT = new PRT_LavaFire
                {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-2.5f, 2.5f),
                        Main.rand.NextFloat(-5f, -2.5f)
                    ),
                    Scale = Main.rand.NextFloat(1.2f, 2f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(255, 180, 100),   // 极亮的橙黄
                        new Color(255, 100, 50),    // 明亮的橙红
                        new Color(200, 50, 30),     // 深红
                        new Color(100, 20, 10)      // 暗红
                    },
                    minLifeTime = 100,
                    maxLifeTime = 180
                };

                PRTLoader.AddParticle(flamePRT);
            }
        }

        /// <summary>
        /// 生成灰烬和火星粒子
        /// </summary>
        private static void SpawnAshAndEmbers()
        {
            // 生成密集的灰烬
            for (int i = 0; i < 5; i++)
            {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-150, Main.screenWidth + 150),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30)
                );

                PRT_LavaFire ashPRT = new PRT_LavaFire
                {
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
            for (int i = 0; i < 3; i++)
            {
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
        private static void SpawnMassiveFlameBurst()
        {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20)
            );

            // 生成环形火焰爆发
            int flameCount = 12;
            for (int i = 0; i < flameCount; i++)
            {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.4f, 0.4f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);

                PRT_LavaFire burstFlame = new PRT_LavaFire
                {
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
            for (int i = 0; i < 20; i++)
            {
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

        /// <summary>
        /// 生成火焰漩涡
        /// </summary>
        private static void SpawnFlameVortex()
        {
            Vector2 vortexCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth / 2f,
                Main.screenPosition.Y + Main.screenHeight / 2f
            );

            // 螺旋状火焰粒子
            int spiralCount = 24;
            for (int i = 0; i < spiralCount; i++)
            {
                float progress = i / (float)spiralCount;
                float angle = progress * MathHelper.TwoPi * 3f; // 3圈螺旋
                float radius = 150f + progress * 250f;
                
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 position = vortexCenter + offset;

                // 检查是否在屏幕范围内
                if (position.X < Main.screenPosition.X - 100 || position.X > Main.screenPosition.X + Main.screenWidth + 100 ||
                    position.Y < Main.screenPosition.Y - 100 || position.Y > Main.screenPosition.Y + Main.screenHeight + 100)
                {
                    continue;
                }

                Vector2 velocity = (vortexCenter - position).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);

                PRT_LavaFire vortexFlame = new PRT_LavaFire
                {
                    Position = position,
                    Velocity = velocity,
                    Scale = Main.rand.NextFloat(1f, 1.8f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(255, 180, 90),
                        new Color(255, 120, 60),
                        new Color(180, 60, 40)
                    },
                    minLifeTime = 80,
                    maxLifeTime = 130
                };

                PRTLoader.AddParticle(vortexFlame);
            }
        }

        public override void Unload()
        {
            IsActive = false;
        }
    }
}
