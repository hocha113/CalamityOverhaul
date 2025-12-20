using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs
{
    ///<summary>
    ///机械场景效果
    ///</summary>
    internal class MachineSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => MachineEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(MachineSky.Name, isActive);
    }

    ///<summary>
    ///机械工业天空效果
    ///</summary>
    internal class MachineSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:MachineSky";
        private bool active;
        private float intensity;

        //机械齿轮转动效果
        private float gearRotation = 0f;
        private float gearSpeed = 0.5f;

        //蒸汽脉冲效果
        private float steamPulse = 0f;
        private float steamIntensity = 0f;

        //电路闪烁效果
        private float circuitFlicker = 0f;

        //全局机械脉动
        private float mechanicalPulse = 0f;

        //机械齿轮闪光
        private readonly MechanicalGear[] gears = new MechanicalGear[8];

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建暗红工业滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.08f, 0.08f)//暗红工业调
                .UseOpacity(0.4f), EffectPriority.High);

            //初始化机械齿轮
            for (int i = 0; i < gears.Length; i++) {
                gears[i] = new MechanicalGear();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            gearRotation = 0f;
            steamPulse = 0f;

            //重置机械齿轮
            for (int i = 0; i < gears.Length; i++) {
                gears[i].Reset();
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            //暗红机械背景
            Color bgColor = new Color(18, 10, 10);
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                bgColor * intensity * 0.9f
            );

            //绘制机械网格线
            DrawMechanicalGrid(spriteBatch);

            //绘制齿轮闪光
            DrawGearFlashes(spriteBatch);

            //绘制蒸汽效果
            if (steamIntensity > 0.05f) {
                DrawSteamEffect(spriteBatch);
            }

            //绘制电路火花
            DrawCircuitSparks(spriteBatch);

            //绘制机械脉冲波
            DrawMechanicalPulse(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = MachineEffect.Cek();

            //强度变化
            if (MachineEffect.IsActive) {
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

            //更新齿轮旋转
            gearRotation += gearSpeed * 0.016f;
            if (gearRotation > MathHelper.TwoPi) {
                gearRotation -= MathHelper.TwoPi;
            }

            //更新机械脉动
            mechanicalPulse += 0.04f;
            if (mechanicalPulse > MathHelper.TwoPi) {
                mechanicalPulse -= MathHelper.TwoPi;
            }

            //更新蒸汽脉冲
            steamPulse += 0.06f;
            steamIntensity = (float)Math.Sin(steamPulse * 0.9f) * 0.5f + 0.2f;

            //更新电路闪烁
            circuitFlicker = (float)Math.Sin(mechanicalPulse * 1.2f) * 0.3f + 0.4f;

            //更新机械齿轮
            for (int i = 0; i < gears.Length; i++) {
                gears[i].Update();
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用暗红工业调
            if (intensity > 0.1f) {
                float mechR = 0.95f;
                float mechG = 0.6f;
                float mechB = 0.6f;

                Color tintedColor = new Color(
                    (int)(inColor.R * mechR),
                    (int)(inColor.G * mechG),
                    (int)(inColor.B * mechB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.35f);
            }
            return inColor;
        }

        #region 绘制特效方法
        private void DrawMechanicalGrid(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //垂直机械栅格
            int gridSpacingV = 120;
            for (int x = 0; x < Main.screenWidth; x += gridSpacingV) {
                float wave = (float)Math.Sin(mechanicalPulse + x * 0.01f) * 0.5f + 0.5f;
                sb.Draw(pixel, new Rectangle(x, 0, 2, Main.screenHeight),
                    new Color(180, 80, 80) * (intensity * 0.12f * wave));
            }

            //水平机械栅格
            int gridSpacingH = 100;
            for (int y = 0; y < Main.screenHeight; y += gridSpacingH) {
                float pulse = (float)Math.Sin(mechanicalPulse + y * 0.008f) * 0.5f + 0.5f;
                sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 2),
                    new Color(180, 80, 80) * (intensity * 0.1f * pulse));
            }

            //运动扫描线
            int scanLineY = (int)((mechanicalPulse / MathHelper.TwoPi) * Main.screenHeight);
            for (int i = -4; i <= 4; i++) {
                int y = scanLineY + i * 3;
                if (y >= 0 && y < Main.screenHeight) {
                    float lineAlpha = 1f - Math.Abs(i) * 0.2f;
                    sb.Draw(pixel, new Rectangle(0, y, Main.screenWidth, 3),
                        new Color(200, 100, 100) * (intensity * 0.25f * lineAlpha));
                }
            }
        }

        private void DrawGearFlashes(SpriteBatch sb) {
            //使用TileHightlight纹理绘制齿轮闪光
            if (CWRAsset.TileHightlight == null || CWRAsset.TileHightlight.IsDisposed) {
                return;
            }

            Texture2D gearTex = CWRAsset.TileHightlight.Value;
            int frameWidth = gearTex.Width / 3;
            int frameHeight = gearTex.Height / 3;

            for (int i = 0; i < gears.Length; i++) {
                MechanicalGear gear = gears[i];
                if (!gear.IsActive) {
                    continue;
                }

                //计算当前帧
                int currentFrame = (int)(gear.AnimProgress * 9);
                if (currentFrame >= 9) {
                    currentFrame = 8;
                }

                int frameX = (currentFrame % 3) * frameWidth;
                int frameY = (currentFrame / 3) * frameHeight;

                Rectangle sourceRect = new Rectangle(frameX, frameY, frameWidth, frameHeight);

                //绘制位置
                Vector2 drawPos = gear.Position - Main.screenPosition;
                float scale = gear.Scale * (1f + (float)Math.Sin(gear.AnimProgress * MathHelper.Pi) * 0.4f);
                float alpha = (float)Math.Sin(gear.AnimProgress * MathHelper.Pi) * intensity * 0.8f;

                Color drawColor = new Color(200, 120, 100, 0) * alpha;

                sb.Draw(
                    gearTex,
                    drawPos,
                    sourceRect,
                    drawColor,
                    gear.Rotation + gearRotation,
                    new Vector2(frameWidth, frameHeight) * 0.5f,
                    scale,
                    SpriteEffects.None,
                    0f
                );

                //额外的发光层
                sb.Draw(
                    gearTex,
                    drawPos,
                    sourceRect,
                    drawColor * 0.6f,
                    gear.Rotation + gearRotation,
                    new Vector2(frameWidth, frameHeight) * 0.5f,
                    scale * 1.3f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawSteamEffect(SpriteBatch sb) {
            //蒸汽脉冲效果
            Color steamColor = new Color(40, 30, 30) * (steamIntensity * intensity * 0.4f);

            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                steamColor);

            //边缘蒸汽增强
            float edgeSteam = (float)Math.Sin(steamPulse * 1.5f) * 0.5f + 0.5f;
            Color edgeColor = new Color(60, 40, 40) * (edgeSteam * intensity * 0.15f);

            //左右边缘
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, 80, Main.screenHeight),
                edgeColor);
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(Main.screenWidth - 80, 0, 80, Main.screenHeight),
                edgeColor);
        }

        private void DrawCircuitSparks(SpriteBatch sb) {
            //电路火花效果
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int sparkCount = (int)(30 * circuitFlicker * intensity);

            for (int i = 0; i < sparkCount; i++) {
                int x = Main.rand.Next(Main.screenWidth);
                int y = Main.rand.Next(Main.screenHeight);
                int size = Main.rand.Next(2, 4);
                float sparkAlpha = Main.rand.NextFloat(0.3f, 0.7f);

                Color sparkColor = new Color(255, 150, 120);
                sb.Draw(pixel,
                    new Rectangle(x, y, size, size),
                    sparkColor * (sparkAlpha * circuitFlicker * intensity));
            }
        }

        private void DrawMechanicalPulse(SpriteBatch sb) {
            //机械脉冲波
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulseSpeed = (float)Main.timeForVisualEffects * 0.03f;

            for (int i = 0; i < 6; i++) {
                float y = (pulseSpeed + i * 0.3f) % 1.2f - 0.1f;
                int screenY = (int)(y * Main.screenHeight);

                if (screenY >= 0 && screenY < Main.screenHeight) {
                    float pulseAlpha = (float)Math.Sin(i * 1.2f + mechanicalPulse) * 0.5f + 0.5f;
                    Color pulseColor = new Color(200, 100, 80) * (pulseAlpha * intensity * 0.18f);

                    sb.Draw(pixel,
                        new Rectangle(0, screenY, Main.screenWidth, 3),
                        pulseColor);
                }
            }
        }
        #endregion

        #region 机械齿轮闪光类
        private class MechanicalGear
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;

            private int cooldown;

            public MechanicalGear() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(40, 160);
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
                AnimSpeed = Main.rand.NextFloat(0.018f, 0.028f);

                //随机位置
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(80, Main.screenWidth - 80),
                    Main.screenPosition.Y + Main.rand.Next(80, Main.screenHeight - 80)
                );

                Scale = Main.rand.NextFloat(1.8f, 3.2f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    ///<summary>
    ///机械场景效果管理器
    ///</summary>
    internal class MachineEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;
        private int sparkTimer = 0;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.MachineEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.MachineEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.MachineEffect);
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

        public static void Start() {
            IsActive = false;
            if (VaultUtils.isServer) {
                return;
            }

            if (CWRWorld.MachineRebellion) {
                IsActive = true;
                return;
            }

            if (!CWRWorld.HasBoss) {
                return;
            }

            if (HeadPrimeAI.DontReform()) {
                return;
            }

            bool found = false;
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.TheDestroyer) {
                    found = true;
                    break;
                }
                else if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism) {
                    found = true;
                    break;
                }
            }
            if (!found) {
                return;
            }

            IsActive = true;
        }

        public override void PostUpdateEverything() {
            if (!Main.gameMenu) {
                Start();
            }

            if (!Cek()) {
                return;
            }

            if (++CekTimer > 60 * 60 * 3) {
                IsActive = false;
                return;
            }

            particleTimer++;
            sparkTimer++;

            //生成机械火花粒子
            if (particleTimer % 6 == 0) {
                SpawnMechanicalSparks();
            }

            //生成齿轮碎片
            if (particleTimer % 15 == 0) {
                SpawnGearFragments();
            }

            //生成蒸汽粒子
            if (sparkTimer % 10 == 0) {
                SpawnSteamParticles();
            }

            //偶尔生成机械爆发
            if (particleTimer % 140 == 0) {
                SpawnMechanicalBurst();
            }

            if (!CWRRef.GetBossRushActive()) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal");
            }
        }

        private static void SpawnMechanicalSparks() {
            //生成机械火花粒子
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(-50, Main.screenWidth + 50),
                Main.screenPosition.Y + Main.rand.Next(-50, Main.screenHeight + 50)
            );

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-0.8f, 0.8f),
                Main.rand.NextFloat(-0.8f, 0.8f)
            );

            PRT_Spark mechanicalSpark = new PRT_Spark(
                spawnPos,
                velocity,
                false,
                Main.rand.Next(70, 130),
                Main.rand.NextFloat(0.6f, 1.2f),
                Color.Lerp(
                    new Color(200, 100, 80),
                    new Color(220, 120, 100),
                    Main.rand.NextFloat()
                )
            );
            PRTLoader.AddParticle(mechanicalSpark);
        }

        private static void SpawnGearFragments() {
            //生成齿轮碎片光点
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
            );

            PRT_Light gearFragment = new PRT_Light(
                spawnPos,
                Vector2.Zero,
                Main.rand.NextFloat(0.8f, 1.4f),
                new Color(180, 90, 70),
                Main.rand.Next(90, 160),
                1f,
                1.2f
            );
            PRTLoader.AddParticle(gearFragment);
        }

        private static void SpawnSteamParticles() {
            //生成从屏幕边缘向外扩散的蒸汽
            int edge = Main.rand.Next(4);
            Vector2 spawnPos;
            Vector2 targetPos = new Vector2(
                Main.screenPosition.X + Main.screenWidth * 0.5f,
                Main.screenPosition.Y + Main.screenHeight * 0.5f
            );

            switch (edge) {
                case 0:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y - 50
                    );
                    break;
                case 1:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight + 50
                    );
                    break;
                case 2:
                    spawnPos = new Vector2(
                        Main.screenPosition.X - 50,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    break;
                default:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.screenWidth + 50,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    break;
            }

            Vector2 velocity = (spawnPos - targetPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.2f, 2.5f);

            PRT_Spark steamSpark = new PRT_Spark(
                spawnPos,
                velocity,
                false,
                Main.rand.Next(90, 150),
                Main.rand.NextFloat(0.7f, 1.3f),
                Color.Lerp(
                    new Color(100, 80, 80),
                    new Color(120, 100, 100),
                    Main.rand.NextFloat()
                )
            );
            PRTLoader.AddParticle(steamSpark);
        }

        private static void SpawnMechanicalBurst() {
            //生成机械爆发效果
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.3f, 0.7f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.3f, 0.7f)
            );

            //环形粒子爆发
            int burstCount = 10;
            for (int i = 0; i < burstCount; i++) {
                float angle = MathHelper.TwoPi * i / burstCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f);

                PRT_Spark burstSpark = new PRT_Spark(
                    burstCenter,
                    velocity,
                    false,
                    Main.rand.Next(50, 90),
                    Main.rand.NextFloat(0.9f, 1.6f),
                    Color.Lerp(
                        new Color(200, 100, 80),
                        new Color(240, 140, 120),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(burstSpark);
            }

            //中心光点
            PRT_Light centralLight = new PRT_Light(
                burstCenter,
                Vector2.Zero,
                Main.rand.NextFloat(1.8f, 2.8f),
                new Color(220, 120, 100),
                70,
                1f,
                2.2f,
                hueShift: 0.01f
            );
            PRTLoader.AddParticle(centralLight);

            //生成次级机械碎片
            for (int i = 0; i < 14; i++) {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(3.5f, 3.5f);

                PRT_Spark fragment = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(18f, 18f),
                    fragmentVelocity,
                    false,
                    Main.rand.Next(40, 80),
                    Main.rand.NextFloat(0.6f, 1.1f),
                    new Color(180, 90, 70)
                );
                PRTLoader.AddParticle(fragment);
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
