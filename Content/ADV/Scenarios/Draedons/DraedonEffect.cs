using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    /// <summary>
    /// 嘉登场景效果（科技风格）
    /// </summary>
    internal class DraedonSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => DraedonEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(DraedonSky.Name, isActive);
    }

    /// <summary>
    /// 嘉登科技天空效果
    /// </summary>
    internal class DraedonSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:DraedonSky";
        private bool active;
        private float intensity;

        //扫描线效果参数
        private float scanLineTimer = 0f;
        private float scanLineSpeed = 0.8f;

        //闪屏效果参数
        private float flickerTimer = 0f;
        private float flickerIntensity = 0f;
        private int flickerCooldown = 0;

        //雪花屏效果参数
        private float noiseTimer = 0f;
        private float noiseIntensity = 0f;

        //全局脉冲
        private float globalPulse = 0f;

        void ICWRLoader.LoadData()
        {
            if (VaultUtils.isServer)
            {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建科技蓝色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.05f, 0.15f, 0.25f)//冷色科技调
                .UseOpacity(0.5f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
            intensity = 0f;
            scanLineTimer = 0f;
            flickerTimer = 0f;
            noiseTimer = 0f;
        }

        public override void Deactivate(params object[] args)
        {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed)
            {
                return;
            }

            //深蓝科技背景
            Color bgColor = new Color(5, 12, 22);
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                bgColor * intensity * 0.85f
            );

            //绘制扫描线网格
            DrawScanLineGrid(spriteBatch);

            //绘制闪屏效果
            if (flickerIntensity > 0.05f)
            {
                DrawFlickerEffect(spriteBatch);
            }

            //绘制雪花屏噪点
            if (noiseIntensity > 0.05f)
            {
                DrawNoiseEffect(spriteBatch);
            }

            //绘制全息数据流
            DrawHologramStream(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset()
        {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime)
        {
            _ = DraedonEffect.Cek();

            //强度变化
            if (DraedonEffect.IsActive)
            {
                if (intensity < 1f)
                {
                    intensity += 0.02f;
                }
            }
            else
            {
                intensity -= 0.015f;
                if (intensity <= 0)
                {
                    Deactivate();
                }
            }

            //更新扫描线
            scanLineTimer += scanLineSpeed * 0.016f;
            if (scanLineTimer > 1f)
            {
                scanLineTimer -= 1f;
            }

            //更新全局脉冲
            globalPulse += 0.03f;
            if (globalPulse > MathHelper.TwoPi)
            {
                globalPulse -= MathHelper.TwoPi;
            }

            //闪屏效果更新
            flickerCooldown--;
            if (flickerCooldown <= 0 && Main.rand.NextBool(300))//随机触发
            {
                flickerTimer = 0f;
                flickerIntensity = Main.rand.NextFloat(0.3f, 0.7f);
                flickerCooldown = Main.rand.Next(180, 360);
            }

            if (flickerIntensity > 0f)
            {
                flickerTimer += 0.15f;
                flickerIntensity *= 0.9f;
            }

            //雪花屏效果
            noiseTimer += 0.08f;
            noiseIntensity = (float)Math.Sin(globalPulse * 0.7f) * 0.05f + 0.03f;
        }

        public override Color OnTileColor(Color inColor)
        {
            //应用冷色科技调
            if (intensity > 0.1f)
            {
                float techR = 0.7f;
                float techG = 0.85f;
                float techB = 1.0f;

                Color tintedColor = new Color(
                    (int)(inColor.R * techR),
                    (int)(inColor.G * techG),
                    (int)(inColor.B * techB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.4f);
            }
            return inColor;
        }

        #region 绘制特效方法
        private void DrawScanLineGrid(SpriteBatch sb)
        {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //垂直扫描线
            int scanY = (int)(scanLineTimer * Main.screenHeight);
            Color scanColor = new Color(60, 180, 255) * (intensity * 0.2f);

            for (int i = -3; i <= 3; i++)
            {
                int y = scanY + i * 2;
                if (y >= 0 && y < Main.screenHeight)
                {
                    float lineAlpha = 1f - Math.Abs(i) * 0.25f;
                    sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 2),
                        scanColor * lineAlpha);
                }
            }

            //水平网格线
            int gridSpacing = 80;
            for (int y = 0; y < Main.screenHeight; y += gridSpacing)
            {
                float wave = (float)Math.Sin(globalPulse + y * 0.01f) * 0.5f + 0.5f;
                sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 1),
                    new Color(40, 120, 180) * (intensity * 0.08f * wave));
            }
        }

        private void DrawFlickerEffect(SpriteBatch sb)
        {
            //模拟屏幕闪烁
            float flicker = (float)Math.Sin(flickerTimer * 30f) * 0.5f + 0.5f;
            Color flashColor = Color.White * (flickerIntensity * flicker * intensity * 0.3f);

            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                flashColor);

            //闪烁时的扫描线扭曲
            if (flicker > 0.7f)
            {
                int distortY = (int)(Main.screenHeight * Main.rand.NextFloat());
                int distortHeight = Main.rand.Next(20, 60);
                sb.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(0, distortY, Main.screenWidth, distortHeight),
                    new Color(100, 200, 255) * (flickerIntensity * 0.4f));
            }
        }

        private void DrawNoiseEffect(SpriteBatch sb)
        {
            //雪花屏噪点效果
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int noiseCount = (int)(150 * noiseIntensity * intensity);

            for (int i = 0; i < noiseCount; i++)
            {
                int x = Main.rand.Next(Main.screenWidth);
                int y = Main.rand.Next(Main.screenHeight);
                int size = Main.rand.Next(1, 4);
                float noiseAlpha = Main.rand.NextFloat(0.3f, 0.8f);

                sb.Draw(pixel,
                    new Rectangle(x, y, size, size),
                    Color.White * (noiseAlpha * noiseIntensity * intensity));
            }
        }

        private void DrawHologramStream(SpriteBatch sb)
        {
            //全息数据流线条
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float streamSpeed = (float)Main.timeForVisualEffects * 0.02f;

            for (int i = 0; i < 8; i++)
            {
                float x = (streamSpeed + i * 0.2f) % 1.2f - 0.1f;
                int screenX = (int)(x * Main.screenWidth);

                if (screenX >= 0 && screenX < Main.screenWidth)
                {
                    float streamAlpha = (float)Math.Sin(i * 0.8f + globalPulse) * 0.5f + 0.5f;
                    Color streamColor = new Color(80, 200, 255) * (streamAlpha * intensity * 0.15f);

                    sb.Draw(pixel,
                        new Rectangle(screenX, 0, 2, Main.screenHeight),
                        streamColor);
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 嘉登场景效果管理器（负责粒子和特效生成）
    /// </summary>
    internal class DraedonEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;
        private int gridFlashTimer = 0;
        private int dataStreamTimer = 0;

        public static bool Cek()
        {
            if (!IsActive)
            {
                CekTimer = 0;
                return false;
            }

            if (Main.gameMenu)
            {
                IsActive = false;
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

            if (++CekTimer > 60 * 60 * 3)//最多持续3分钟
            {
                IsActive = false;
                return;
            }

            particleTimer++;
            gridFlashTimer++;
            dataStreamTimer++;

            //生成数据粒子
            if (particleTimer % 3 == 0)
            {
                SpawnDataParticles();
            }

            //生成电路节点
            if (particleTimer % 8 == 0)
            {
                SpawnCircuitNodes();
            }

            //生成网格闪光效果
            if (gridFlashTimer >= 45)
            {
                SpawnGridFlash();
                gridFlashTimer = 0;
            }

            //生成数据流
            if (dataStreamTimer % 5 == 0)
            {
                SpawnDataStream();
            }

            //偶尔生成科技爆发
            if (particleTimer % 90 == 0)
            {
                SpawnTechBurst();
            }
        }

        private static void SpawnDataParticles()
        {
            //生成科技数据粒子
            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-50, Main.screenWidth + 50),
                    Main.screenPosition.Y + Main.rand.Next(-50, Main.screenHeight + 50)
                );

                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-0.8f, 0.8f),
                    Main.rand.NextFloat(-0.8f, 0.8f)
                );

                PRT_Spark dataSpark = new PRT_Spark(
                    spawnPos,
                    velocity,
                    false,
                    Main.rand.Next(60, 120),
                    Main.rand.NextFloat(0.6f, 1.2f),
                    Color.Lerp(
                        new Color(80, 200, 255),
                        new Color(100, 220, 255),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(dataSpark);
            }
        }

        private static void SpawnCircuitNodes()
        {
            //生成电路节点光点
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
            );

            PRT_Light circuitNode = new PRT_Light(
                spawnPos,
                Vector2.Zero,
                Main.rand.NextFloat(0.8f, 1.5f),
                new Color(100, 220, 255),
                Main.rand.Next(80, 150),
                1f,
                1.2f
            );
            PRTLoader.AddParticle(circuitNode);
        }

        private static void SpawnGridFlash()
        {
            //使用3*3帧纹理生成网格闪光
            if (CWRAsset.TileHightlight == null)
            {
                return;
            }

            Vector2 gridPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(100, Main.screenWidth - 100),
                Main.screenPosition.Y + Main.rand.Next(100, Main.screenHeight - 100)
            );

            //创建网格闪光粒子
            int gridSize = Main.rand.Next(30, 60);
            Color gridColor = new Color(80, 200, 255) * 0.8f;

            PRT_Light gridFlash = new PRT_Light(
                gridPos,
                Vector2.Zero,
                Main.rand.NextFloat(1.5f, 2.5f),
                gridColor,
                Main.rand.Next(40, 80),
                1f,
                1.8f,
                hueShift: 0.02f
            );
            PRTLoader.AddParticle(gridFlash);

            //周围生成小型数据点
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(20f, 40f);

                PRT_Spark miniSpark = new PRT_Spark(
                    gridPos + offset,
                    Vector2.Zero,
                    false,
                    Main.rand.Next(30, 60),
                    Main.rand.NextFloat(0.5f, 1f),
                    new Color(100, 220, 255)
                );
                PRTLoader.AddParticle(miniSpark);
            }
        }

        private static void SpawnDataStream()
        {
            //生成从屏幕边缘流向中心的数据流
            int edge = Main.rand.Next(4);
            Vector2 spawnPos;
            Vector2 targetPos = new Vector2(
                Main.screenPosition.X + Main.screenWidth * 0.5f,
                Main.screenPosition.Y + Main.screenHeight * 0.5f
            );

            switch (edge)
            {
                case 0://上
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y - 50
                    );
                    break;
                case 1://下
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight + 50
                    );
                    break;
                case 2://左
                    spawnPos = new Vector2(
                        Main.screenPosition.X - 50,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    break;
                default://右
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.screenWidth + 50,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    break;
            }

            Vector2 velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);

            PRT_Spark streamSpark = new PRT_Spark(
                spawnPos,
                velocity,
                false,
                Main.rand.Next(80, 140),
                Main.rand.NextFloat(0.8f, 1.4f),
                Color.Lerp(
                    new Color(60, 180, 255),
                    new Color(100, 220, 255),
                    Main.rand.NextFloat()
                )
            );
            PRTLoader.AddParticle(streamSpark);
        }

        private static void SpawnTechBurst()
        {
            //生成科技爆发效果
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.2f, 0.8f)
            );

            //环形粒子爆发
            int burstCount = 12;
            for (int i = 0; i < burstCount; i++)
            {
                float angle = MathHelper.TwoPi * i / burstCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                PRT_Spark burstSpark = new PRT_Spark(
                    burstCenter,
                    velocity,
                    false,
                    Main.rand.Next(50, 90),
                    Main.rand.NextFloat(1f, 1.8f),
                    Color.Lerp(
                        new Color(80, 200, 255),
                        new Color(120, 240, 255),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(burstSpark);
            }

            //中心光点
            PRT_Light centralLight = new PRT_Light(
                burstCenter,
                Vector2.Zero,
                Main.rand.NextFloat(2f, 3f),
                new Color(100, 220, 255),
                60,
                1f,
                2.5f,
                hueShift: 0.03f
            );
            PRTLoader.AddParticle(centralLight);

            //生成次级数据碎片
            for (int i = 0; i < 20; i++)
            {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(4f, 4f);

                PRT_Spark fragment = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(20f, 20f),
                    fragmentVelocity,
                    false,
                    Main.rand.Next(40, 80),
                    Main.rand.NextFloat(0.6f, 1.2f),
                    new Color(60, 180, 240)
                );
                PRTLoader.AddParticle(fragment);
            }
        }

        public override void Unload()
        {
            IsActive = false;
        }
    }
}
