using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    /// <summary>
    /// 嘉登场景效果
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

        //柔化的闪烁效果参数
        private float softFlickerTimer = 0f;
        private float softFlickerIntensity = 0f;

        //雪花屏效果参数
        private float noiseIntensity = 0f;

        //全局脉冲
        private float globalPulse = 0f;

        //科技格子闪光效果
        private readonly TechGridFlash[] techGrids = new TechGridFlash[6];

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建科技蓝色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.05f, 0.15f, 0.25f)//冷色科技调
                .UseOpacity(0.5f), EffectPriority.High);

            //初始化科技格子
            for (int i = 0; i < techGrids.Length; i++) {
                techGrids[i] = new TechGridFlash();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            scanLineTimer = 0f;
            softFlickerTimer = 0f;

            //重置科技格子
            for (int i = 0; i < techGrids.Length; i++) {
                techGrids[i].Reset();
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
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

            //绘制科技格子闪光（使用TileHightlight纹理）
            DrawTechGridFlashes(spriteBatch);

            //绘制柔和闪烁效果（移除刺眼的闪屏）
            if (softFlickerIntensity > 0.05f) {
                DrawSoftFlickerEffect(spriteBatch);
            }

            //绘制雪花屏噪点（降低强度）
            if (noiseIntensity > 0.05f) {
                DrawNoiseEffect(spriteBatch);
            }

            //绘制全息数据流
            DrawHologramStream(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = DraedonEffect.Cek();

            //强度变化
            if (DraedonEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.02f;
                }
            }
            else {
                intensity -= 0.015f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }

            //更新扫描线
            scanLineTimer += scanLineSpeed * 0.016f;
            if (scanLineTimer > 1f) {
                scanLineTimer -= 1f;
            }

            //更新全局脉冲
            globalPulse += 0.03f;
            if (globalPulse > MathHelper.TwoPi) {
                globalPulse -= MathHelper.TwoPi;
            }

            //更新柔和闪烁（持续微弱的脉冲，不刺眼）
            softFlickerTimer += 0.05f;
            softFlickerIntensity = (float)Math.Sin(softFlickerTimer * 0.8f) * 0.5f + 0.15f;//0.0-0.65范围

            //雪花屏效果
            noiseIntensity = (float)Math.Sin(globalPulse * 0.7f) * 0.02f + 0.02f;//降低到0.0-0.04范围

            //更新科技格子
            for (int i = 0; i < techGrids.Length; i++) {
                techGrids[i].Update();
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用冷色科技调
            if (intensity > 0.1f) {
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
        private void DrawScanLineGrid(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //垂直扫描线
            int scanY = (int)(scanLineTimer * Main.screenHeight);
            Color scanColor = new Color(60, 180, 255) * (intensity * 0.2f);

            for (int i = -3; i <= 3; i++) {
                int y = scanY + i * 2;
                if (y >= 0 && y < Main.screenHeight) {
                    float lineAlpha = 1f - Math.Abs(i) * 0.25f;
                    sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 2),
                        scanColor * lineAlpha);
                }
            }

            //水平网格线
            int gridSpacing = 80;
            for (int y = 0; y < Main.screenHeight; y += gridSpacing) {
                float wave = (float)Math.Sin(globalPulse + y * 0.01f) * 0.5f + 0.5f;
                sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 1),
                    new Color(40, 120, 180) * (intensity * 0.08f * wave));
            }
        }

        private void DrawTechGridFlashes(SpriteBatch sb) {
            //使用TileHightlight纹理绘制科技格子闪光
            if (CWRAsset.TileHightlight == null || CWRAsset.TileHightlight.IsDisposed) {
                return;
            }

            Texture2D gridTex = CWRAsset.TileHightlight.Value;
            int frameWidth = gridTex.Width / 3;//3列
            int frameHeight = gridTex.Height / 3;//3行

            for (int i = 0; i < techGrids.Length; i++) {
                TechGridFlash grid = techGrids[i];
                if (!grid.IsActive) {
                    continue;
                }

                //计算当前帧（3*3=9帧）
                int currentFrame = (int)(grid.AnimProgress * 9);
                if (currentFrame >= 9) {
                    currentFrame = 8;
                }

                int frameX = (currentFrame % 3) * frameWidth;
                int frameY = (currentFrame / 3) * frameHeight;

                Rectangle sourceRect = new Rectangle(frameX, frameY, frameWidth, frameHeight);

                //绘制位置
                Vector2 drawPos = grid.Position - Main.screenPosition;
                float scale = grid.Scale * (1f + (float)Math.Sin(grid.AnimProgress * MathHelper.Pi) * 0.3f);
                float alpha = (float)Math.Sin(grid.AnimProgress * MathHelper.Pi) * intensity * 0.7f;

                Color drawColor = new Color(80, 200, 255, 0) * alpha;

                sb.Draw(
                    gridTex,
                    drawPos,
                    sourceRect,
                    drawColor,
                    grid.Rotation,
                    new Vector2(frameWidth, frameHeight) * 0.5f,
                    scale,
                    SpriteEffects.None,
                    0f
                );

                //额外的发光层
                sb.Draw(
                    gridTex,
                    drawPos,
                    sourceRect,
                    drawColor * 0.5f,
                    grid.Rotation,
                    new Vector2(frameWidth, frameHeight) * 0.5f,
                    scale * 1.2f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawSoftFlickerEffect(SpriteBatch sb) {
            //柔和的闪烁效果，不会刺眼
            Color flickerColor = new Color(20, 60, 90) * (softFlickerIntensity * intensity * 0.5f);

            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                flickerColor);

            //柔和的边缘亮度
            float edgeBrightness = (float)Math.Sin(softFlickerTimer * 1.2f) * 0.5f + 0.5f;
            Color edgeColor = new Color(40, 120, 180) * (edgeBrightness * intensity * 0.1f);

            //上下边缘
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, 50),
                edgeColor);
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, Main.screenHeight - 50, Main.screenWidth, 50),
                edgeColor);
        }

        private void DrawNoiseEffect(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int noiseCount = (int)(50 * noiseIntensity * intensity);

            for (int i = 0; i < noiseCount; i++) {
                int x = Main.rand.Next(Main.screenWidth);
                int y = Main.rand.Next(Main.screenHeight);
                int size = Main.rand.Next(1, 3);
                float noiseAlpha = Main.rand.NextFloat(0.2f, 0.5f);

                sb.Draw(pixel,
                    new Rectangle(x, y, size, size),
                    Color.White * (noiseAlpha * noiseIntensity * intensity));
            }
        }

        private void DrawHologramStream(SpriteBatch sb) {
            //全息数据流线条
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float streamSpeed = (float)Main.timeForVisualEffects * 0.02f;

            for (int i = 0; i < 8; i++) {
                float x = (streamSpeed + i * 0.2f) % 1.2f - 0.1f;
                int screenX = (int)(x * Main.screenWidth);

                if (screenX >= 0 && screenX < Main.screenWidth) {
                    float streamAlpha = (float)Math.Sin(i * 0.8f + globalPulse) * 0.5f + 0.5f;
                    Color streamColor = new Color(80, 200, 255) * (streamAlpha * intensity * 0.15f);

                    sb.Draw(pixel,
                        new Rectangle(screenX, 0, 2, Main.screenHeight),
                        streamColor);
                }
            }
        }
        #endregion

        #region 科技格子闪光类
        private class TechGridFlash
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;

            private int cooldown;

            public TechGridFlash() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(60, 180);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) {
                        Activate();
                    }
                    return;
                }

                AnimProgress += AnimSpeed;
                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.015f, 0.025f);

                //随机位置
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(100, Main.screenWidth - 100),
                    Main.screenPosition.Y + Main.rand.Next(100, Main.screenHeight - 100)
                );

                Scale = Main.rand.NextFloat(1.5f, 3f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    /// <summary>
    /// 嘉登场景效果管理器
    /// </summary>
    internal class DraedonEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;
        private int dataStreamTimer = 0;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.DraedonEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.DraedonEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.DraedonEffect);
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
                return;
            }

            if (++CekTimer > 60 * 60 * 3)//最多持续3分钟
            {
                IsActive = false;
                return;
            }

            particleTimer++;
            dataStreamTimer++;

            //生成数据粒子（降低频率）
            if (particleTimer % 5 == 0) {
                SpawnDataParticles();
            }

            //生成电路节点（降低频率）
            if (particleTimer % 12 == 0) {
                SpawnCircuitNodes();
            }

            //生成数据流
            if (dataStreamTimer % 8 == 0) {
                SpawnDataStream();
            }

            //偶尔生成科技爆发
            if (particleTimer % 120 == 0) {
                SpawnTechBurst();
            }

            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityMod/Sounds/Music/DraedonExoSelect");
        }

        private static void SpawnDataParticles() {
            //生成科技数据粒子（减少数量）
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(-50, Main.screenWidth + 50),
                Main.screenPosition.Y + Main.rand.Next(-50, Main.screenHeight + 50)
            );

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-0.5f, 0.5f),
                Main.rand.NextFloat(-0.5f, 0.5f)
            );

            PRT_Spark dataSpark = new PRT_Spark(
                spawnPos,
                velocity,
                false,
                Main.rand.Next(80, 150),
                Main.rand.NextFloat(0.5f, 1f),
                Color.Lerp(
                    new Color(80, 200, 255),
                    new Color(100, 220, 255),
                    Main.rand.NextFloat()
                )
            );
            PRTLoader.AddParticle(dataSpark);
        }

        private static void SpawnCircuitNodes() {
            //生成电路节点光点
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
            );

            PRT_Light circuitNode = new PRT_Light(
                spawnPos,
                Vector2.Zero,
                Main.rand.NextFloat(0.7f, 1.2f),
                new Color(100, 220, 255),
                Main.rand.Next(100, 180),
                1f,
                1f
            );
            PRTLoader.AddParticle(circuitNode);
        }

        private static void SpawnDataStream() {
            //生成从屏幕边缘流向中心的数据流
            int edge = Main.rand.Next(4);
            Vector2 spawnPos;
            Vector2 targetPos = new Vector2(
                Main.screenPosition.X + Main.screenWidth * 0.5f,
                Main.screenPosition.Y + Main.screenHeight * 0.5f
            );

            switch (edge) {
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

            Vector2 velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3f);

            PRT_Spark streamSpark = new PRT_Spark(
                spawnPos,
                velocity,
                false,
                Main.rand.Next(100, 160),
                Main.rand.NextFloat(0.6f, 1.2f),
                Color.Lerp(
                    new Color(60, 180, 255),
                    new Color(100, 220, 255),
                    Main.rand.NextFloat()
                )
            );
            PRTLoader.AddParticle(streamSpark);
        }

        private static void SpawnTechBurst() {
            //生成科技爆发效果
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.3f, 0.7f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.3f, 0.7f)
            );

            //环形粒子爆发（减少数量）
            int burstCount = 8;
            for (int i = 0; i < burstCount; i++) {
                float angle = MathHelper.TwoPi * i / burstCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                PRT_Spark burstSpark = new PRT_Spark(
                    burstCenter,
                    velocity,
                    false,
                    Main.rand.Next(60, 100),
                    Main.rand.NextFloat(0.8f, 1.5f),
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
                Main.rand.NextFloat(1.5f, 2.5f),
                new Color(100, 220, 255),
                80,
                1f,
                2f,
                hueShift: 0.02f
            );
            PRTLoader.AddParticle(centralLight);

            //生成次级数据碎片（减少数量）
            for (int i = 0; i < 12; i++) {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(3f, 3f);

                PRT_Spark fragment = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(15f, 15f),
                    fragmentVelocity,
                    false,
                    Main.rand.Next(50, 90),
                    Main.rand.NextFloat(0.5f, 1f),
                    new Color(60, 180, 240)
                );
                PRTLoader.AddParticle(fragment);
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
