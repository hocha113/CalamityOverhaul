using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    /// <summary>
    /// 硫磺海场景效果
    /// </summary>
    internal class OldDukeSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => OldDukeEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SulfurSeaSky.Name, isActive);
    }

    /// <summary>
    /// 硫磺海天空效果
    /// </summary>
    internal class SulfurSeaSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:SulfurSeaSky";
        private bool active;
        private float intensity;

        //毒雾效果参数
        private float toxicWaveTimer = 0f;
        private float acidPulseTimer = 0f;
        private float corrosionTimer = 0f;

        //毒雾层
        private readonly ToxicMist[] mists = new ToxicMist[100];

        //酸液气泡
        private readonly AcidBubble[] bubbles = new AcidBubble[30];

        //腐蚀斑块
        private readonly CorrosionPatch[] patches = new CorrosionPatch[12];

        //硫磺海配色
        private readonly Color[] sulfurColors = new Color[]
        {
            new Color(120, 220, 140),   //亮毒绿
            new Color(100, 190, 110),   //标准毒绿
            new Color(80, 170, 90),     //深毒绿
            new Color(150, 200, 100),   //黄绿色
            new Color(90, 180, 100),    //暗绿色
        };

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建硫磺海毒绿色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.25f, 0.15f)//毒绿色调
                .UseOpacity(0.6f), EffectPriority.High);

            //初始化毒雾
            for (int i = 0; i < mists.Length; i++) {
                mists[i] = new ToxicMist();
            }

            //初始化气泡
            for (int i = 0; i < bubbles.Length; i++) {
                bubbles[i] = new AcidBubble();
            }

            //初始化腐蚀斑块
            for (int i = 0; i < patches.Length; i++) {
                patches[i] = new CorrosionPatch();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            toxicWaveTimer = 0f;
            acidPulseTimer = 0f;
            corrosionTimer = 0f;

            //重置毒雾
            for (int i = 0; i < mists.Length; i++) {
                mists[i].Reset();
            }

            //重置气泡
            for (int i = 0; i < bubbles.Length; i++) {
                bubbles[i].Reset();
            }

            //重置腐蚀斑块
            for (int i = 0; i < patches.Length; i++) {
                patches[i].Reset();
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            //污染的暗绿背景
            Color bgColor = new Color(15, 25, 18);
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                bgColor * intensity * 0.95f
            );

            //绘制毒雾层（多层次）
            DrawToxicMists(spriteBatch);

            //绘制上升的酸液气泡
            DrawAcidBubbles(spriteBatch);

            //绘制腐蚀斑块
            DrawCorrosionPatches(spriteBatch);

            //绘制毒液波纹
            DrawToxicWaves(spriteBatch);

            //绘制酸雾脉冲效果
            DrawAcidPulse(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            //强度变化
            if (OldDukeEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.025f;
                }
            }
            else {
                intensity -= 0.02f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }

            //更新毒液波动
            toxicWaveTimer += 0.035f;
            if (toxicWaveTimer > MathHelper.TwoPi) {
                toxicWaveTimer -= MathHelper.TwoPi;
            }

            //更新酸性脉冲
            acidPulseTimer += 0.045f;
            if (acidPulseTimer > MathHelper.TwoPi) {
                acidPulseTimer -= MathHelper.TwoPi;
            }

            //更新腐蚀效果
            corrosionTimer += 0.025f;
            if (corrosionTimer > MathHelper.TwoPi) {
                corrosionTimer -= MathHelper.TwoPi;
            }

            //更新毒雾
            for (int i = 0; i < mists.Length; i++) {
                mists[i].Update();
            }

            //更新气泡
            for (int i = 0; i < bubbles.Length; i++) {
                bubbles[i].Update();
            }

            //更新腐蚀斑块
            for (int i = 0; i < patches.Length; i++) {
                patches[i].Update();
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用毒绿色调
            if (intensity > 0.1f) {
                float toxicR = 0.85f;
                float toxicG = 1.0f;
                float toxicB = 0.8f;

                Color tintedColor = new Color(
                    (int)(inColor.R * toxicR),
                    (int)(inColor.G * toxicG),
                    (int)(inColor.B * toxicB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }
            return inColor;
        }

        #region 绘制特效方法
        private void DrawToxicMists(SpriteBatch sb) {
            if (CWRAsset.Fog == null || CWRAsset.Fog.IsDisposed) {
                return;
            }

            Texture2D mistTex = CWRAsset.Fog.Value;

            for (int i = 0; i < mists.Length; i++) {
                ToxicMist mist = mists[i];
                if (!mist.IsActive) {
                    continue;
                }

                Vector2 drawPos = mist.Position - Main.screenPosition;

                //根据层次和时间变换毒绿色
                int colorIndex = (int)(toxicWaveTimer * 2f + i * 0.1f) % sulfurColors.Length;
                Color mistColor = Color.Lerp(sulfurColors[colorIndex],
                    sulfurColors[(colorIndex + 1) % sulfurColors.Length],
                    (float)Math.Sin(toxicWaveTimer * 1.5f + i * 0.3f) * 0.5f + 0.5f);

                //根据深度调整透明度
                float depthAlpha = mist.Depth * 0.4f;
                float alpha = (float)Math.Sin(mist.AnimProgress * MathHelper.Pi) * intensity * depthAlpha;

                sb.Draw(
                    mistTex,
                    drawPos,
                    null,
                    mistColor * alpha,
                    mist.Rotation,
                    mistTex.Size() * 0.5f,
                    mist.Scale,
                    SpriteEffects.None,
                    0f
                );

                //添加毒性发光层
                sb.Draw(
                    mistTex,
                    drawPos,
                    null,
                    mistColor * alpha * 0.6f,
                    mist.Rotation * 0.7f,
                    mistTex.Size() * 0.5f,
                    mist.Scale * 1.4f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawAcidBubbles(SpriteBatch sb) {
            if (CWRAsset.TileHightlight == null || CWRAsset.TileHightlight.IsDisposed) {
                return;
            }

            Texture2D bubbleTex = CWRAsset.TileHightlight.Value;
            int frameWidth = bubbleTex.Width / 3;
            int frameHeight = bubbleTex.Height / 3;

            for (int i = 0; i < bubbles.Length; i++) {
                AcidBubble bubble = bubbles[i];
                if (!bubble.IsActive) {
                    continue;
                }

                int currentFrame = (int)(bubble.AnimProgress * 9);
                if (currentFrame >= 9) {
                    currentFrame = 8;
                }

                int frameX = (currentFrame % 3) * frameWidth;
                int frameY = (currentFrame / 3) * frameHeight;
                Rectangle sourceRect = new Rectangle(frameX, frameY, frameWidth, frameHeight);

                Vector2 drawPos = bubble.Position - Main.screenPosition;

                //气泡颜色随波动变化
                float colorShift = (float)Math.Sin(acidPulseTimer * 2f + i * 0.5f) * 0.5f + 0.5f;
                Color bubbleColor = Color.Lerp(
                    new Color(120, 220, 140, 200),
                    new Color(150, 200, 100, 220),
                    colorShift
                );

                float alpha = (float)Math.Sin(bubble.AnimProgress * MathHelper.Pi) * intensity * 0.7f;
                float scale = bubble.Scale * (1f + (float)Math.Sin(bubble.AnimProgress * MathHelper.Pi * 3f) * 0.15f);

                //绘制多层气泡效果
                for (int z = 0; z < 3; z++) {
                    sb.Draw(
                        bubbleTex,
                        drawPos,
                        sourceRect,
                        bubbleColor * alpha * (1f - z * 0.3f),
                        bubble.Rotation + z * 0.2f,
                        new Vector2(frameWidth, frameHeight) * 0.5f,
                        scale + z * 0.15f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawCorrosionPatches(SpriteBatch sb) {
            Texture2D pixel = CWRAsset.SoftGlow.Value;

            for (int i = 0; i < patches.Length; i++) {
                CorrosionPatch patch = patches[i];
                if (!patch.IsActive) {
                    continue;
                }

                Vector2 drawPos = patch.Position - Main.screenPosition;

                //腐蚀斑块的脉动效果
                float pulsate = (float)Math.Sin(corrosionTimer * 1.5f + i * 0.8f) * 0.5f + 0.5f;
                Color patchColor = Color.Lerp(
                    new Color(70, 140, 80),
                    new Color(100, 180, 100),
                    pulsate
                );

                float alpha = (float)Math.Sin(patch.AnimProgress * MathHelper.Pi) * intensity * 0.4f;
                float scale = patch.Scale * (1f + pulsate * 0.3f);

                //绘制腐蚀斑块的扩散效果
                for (int ring = 0; ring < 15; ring++) {
                    float ringScale = scale * (1f + ring * 0.2f);
                    float ringAlpha = alpha * (1f - ring * 0.2f);

                    sb.Draw(pixel,
                        drawPos,
                        null,
                        patchColor with { A = 0 } * ringAlpha,
                        0f,
                        pixel.Size() / 2,
                        ringScale,
                        SpriteEffects.None,
                        0f);
                }
            }
        }

        private void DrawToxicWaves(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //水平毒液波纹
            int waveCount = 4;
            for (int i = 0; i < waveCount; i++) {
                float phase = (toxicWaveTimer + i * MathHelper.TwoPi / waveCount) % MathHelper.TwoPi;
                float waveAlpha = (float)Math.Sin(phase) * 0.5f + 0.5f;

                int colorIndex = (int)(phase * 2f) % sulfurColors.Length;
                Color waveColor = sulfurColors[colorIndex];

                int y = (int)(Main.screenHeight * (0.2f + i * 0.2f));
                
                //绘制波动的毒液线
                for (int x = 0; x < Main.screenWidth; x += 8) {
                    float waveOffset = (float)Math.Sin((x * 0.01f) + toxicWaveTimer * 2f) * 20f;
                    int waveY = y + (int)waveOffset;

                    sb.Draw(pixel,
                        new Rectangle(x, waveY, 8, 3),
                        waveColor * (waveAlpha * intensity * 0.25f));
                }
            }
        }

        private void DrawAcidPulse(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //中心酸性脉冲
            float pulseIntensity = (float)Math.Sin(acidPulseTimer * 1.2f) * 0.5f + 0.5f;
            int colorIndex = (int)(acidPulseTimer * 1.5f) % sulfurColors.Length;
            Color pulseColor = Color.Lerp(sulfurColors[colorIndex],
                sulfurColors[(colorIndex + 1) % sulfurColors.Length],
                pulseIntensity);

            Vector2 centerPos = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float radius = (float)Math.Sin(acidPulseTimer * 0.8f) * 120f + 250f;

            //环形酸性脉冲
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f + acidPulseTimer * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * radius;

                sb.Draw(pixel,
                    centerPos + offset,
                    new Rectangle(0, 0, 4, 4),
                    pulseColor * (pulseIntensity * intensity * 0.3f));
            }
        }
        #endregion

        #region 毒雾类
        private class ToxicMist
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public float Depth; //深度层次
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public ToxicMist() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 100);
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
                Position += Velocity;
                Rotation += 0.003f * Depth;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.002f, 0.006f);
                Depth = Main.rand.NextFloat(0.3f, 1f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-300, Main.screenWidth + 300),
                    Main.screenPosition.Y + Main.rand.Next(-300, Main.screenHeight + 300)
                );

                Velocity = Main.rand.NextVector2Circular(0.4f, 0.4f) * Depth;
                Scale = Main.rand.NextFloat(1.5f, 4f) * (0.5f + Depth * 0.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 酸液气泡类
        private class AcidBubble
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public AcidBubble() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(40, 150);
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
                Position += Velocity;
                Rotation += 0.008f;

                //气泡上升时的横向漂移
                Velocity.X += (float)Math.Sin(AnimProgress * 10f) * 0.05f;

                if (AnimProgress >= 1f || Position.Y < Main.screenPosition.Y - 100) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.008f, 0.015f);

                //从底部生成
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(0, 100)
                );

                //向上浮动
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.2f, 0.2f),
                    Main.rand.NextFloat(-1.2f, -0.7f)
                );

                Scale = Main.rand.NextFloat(0.8f, 1.8f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 腐蚀斑块类
        private class CorrosionPatch
        {
            public Vector2 Position;
            public float Scale;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;

            private int cooldown;

            public CorrosionPatch() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(80, 250);
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
                AnimSpeed = Main.rand.NextFloat(0.006f, 0.012f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(150, Main.screenWidth - 150),
                    Main.screenPosition.Y + Main.rand.Next(150, Main.screenHeight - 150)
                );

                Scale = Main.rand.NextFloat(1.2f, 2.8f);
            }
        }
        #endregion
    }

    /// <summary>
    /// 硫磺海场景效果管理器
    /// </summary>
    internal class OldDukeEffect : ModSystem
    {
        public static bool IsActive;
        public static int ActiveTimer;
        
        private int toxicBubbleTimer = 0;
        private int acidMistTimer = 0;
        private int corrosionTimer = 0;
        private int poisonWaveTimer = 0;

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.OldDukeEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.OldDukeEffect);
                    packet.Write(IsActive);
                    packet.Send(-1, whoAmI);
                }
            }
        }

        public override void PostUpdateEverything() {
            if (!IsActive) {
                if (Main.LocalPlayer.TryGetADVSave(out var save) && !save.OldDukeChoseToFight && NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    IsActive = true;
                    Send();
                }
            }
            else {
                if (!NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    IsActive = false;
                    Send();
                }
            }

            if (IsActive) {
                ActiveTimer++;
                
                toxicBubbleTimer++;
                acidMistTimer++;
                corrosionTimer++;
                poisonWaveTimer++;

                //生成毒泡
                if (toxicBubbleTimer % 3 == 0) {
                    SpawnToxicBubbles();
                }

                //生成酸雾
                if (acidMistTimer % 6 == 0) {
                    SpawnAcidMist();
                }

                //生成腐蚀粒子
                if (corrosionTimer % 8 == 0) {
                    SpawnCorrosionParticles();
                }

                //生成毒液波纹
                if (poisonWaveTimer % 90 == 0) {
                    SpawnPoisonWave();
                }

                //偶尔生成硫酸爆发效果
                if (ActiveTimer % 150 == 0) {
                    SpawnSulfuricBurst();
                }

                //播放硫磺海音乐
                if (!CWRRef.GetBossRushActive()) {
                    Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityModMusic/Sounds/Music/AcidRainTier1");
                }

                //超时保护（3分钟）
                if (ActiveTimer > 60 * 60 * 3) {
                    IsActive = false;
                    ActiveTimer = 0;
                }
            }
            else {
                ActiveTimer = 0;
                toxicBubbleTimer = 0;
                acidMistTimer = 0;
                corrosionTimer = 0;
                poisonWaveTimer = 0;
            }
        }

        /// <summary>
        /// 生成从底部上升的毒泡
        /// </summary>
        private static void SpawnToxicBubbles() {
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(0, 100)
            );

            PRT_ToxicBubble toxicBubble = new PRT_ToxicBubble(
                spawnPos,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.5f, -0.8f)),
                Main.rand.NextFloat(0.8f, 1.6f),
                Main.rand.Next(120, 200)
            );
            PRTLoader.AddParticle(toxicBubble);
        }

        /// <summary>
        /// 生成弥漫的酸雾效果
        /// </summary>
        private static void SpawnAcidMist() {
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
            );

            Vector2 velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
            float depth = Main.rand.NextFloat(0.3f, 1f);

            PRT_ToxicMist acidMist = new PRT_ToxicMist(
                spawnPos,
                velocity,
                Main.rand.NextFloat(2.5f, 4.5f),
                Main.rand.Next(150, 250),
                depth
            );
            PRTLoader.AddParticle(acidMist);
        }

        /// <summary>
        /// 生成腐蚀性粒子效果
        /// </summary>
        private static void SpawnCorrosionParticles() {
            int edge = Main.rand.Next(4);
            Vector2 spawnPos;
            Vector2 targetDirection;

            switch (edge) {
                case 0:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y - 80
                    );
                    targetDirection = Vector2.UnitY;
                    break;
                case 1:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight + 80
                    );
                    targetDirection = -Vector2.UnitY;
                    break;
                case 2:
                    spawnPos = new Vector2(
                        Main.screenPosition.X - 80,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    targetDirection = Vector2.UnitX;
                    break;
                default:
                    spawnPos = new Vector2(
                        Main.screenPosition.X + Main.screenWidth + 80,
                        Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
                    );
                    targetDirection = -Vector2.UnitX;
                    break;
            }

            Vector2 velocity = targetDirection.RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 2.5f);

            PRT_AcidSplash corrosionSpark = new PRT_AcidSplash(
                spawnPos,
                velocity,
                Main.rand.NextFloat(0.7f, 1.4f),
                Main.rand.Next(100, 180),
                false //从边缘飞入不受重力影响
            );
            PRTLoader.AddParticle(corrosionSpark);
        }

        /// <summary>
        /// 生成扩散的毒液波纹
        /// </summary>
        private static void SpawnPoisonWave() {
            Vector2 waveCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.25f, 0.75f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.25f, 0.75f)
            );

            //生成主波纹
            PRT_CorrosionWave mainWave = new PRT_CorrosionWave(
                waveCenter,
                0.2f,
                1f,
                90,
                Main.rand.NextFloat(MathHelper.TwoPi)
            );
            PRTLoader.AddParticle(mainWave);

            //生成次级波纹
            for (int i = 0; i < 2; i++) {
                PRT_CorrosionWave secondaryWave = new PRT_CorrosionWave(
                    waveCenter,
                    0.1f,
                    0.6f,
                    70,
                    Main.rand.NextFloat(MathHelper.TwoPi)
                );
                PRTLoader.AddParticle(secondaryWave);
            }

            //环形酸液飞溅
            int waveCount = 16;
            for (int i = 0; i < waveCount; i++) {
                float angle = MathHelper.TwoPi * i / waveCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                PRT_AcidSplash waveSplash = new PRT_AcidSplash(
                    waveCenter,
                    velocity,
                    Main.rand.NextFloat(1f, 2f),
                    Main.rand.Next(70, 120),
                    false
                );
                PRTLoader.AddParticle(waveSplash);
            }

            //中心发光核心
            PRT_SulfuricCore centerCore = new PRT_SulfuricCore(
                waveCenter,
                Main.rand.NextFloat(0.15f, 0.5f),
                60
            );
            PRTLoader.AddParticle(centerCore);

            //播放水泡音效
            if (Main.rand.NextBool(4)) {
                SoundEngine.PlaySound(SoundID.Item21 with { 
                    Volume = 0.3f, 
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, waveCenter);
            }
        }

        /// <summary>
        /// 生成硫酸爆发效果
        /// </summary>
        private static void SpawnSulfuricBurst() {
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.2f, 0.8f),
                Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.2f, 0.8f)
            );

            //爆发核心
            PRT_SulfuricCore burstCore = new PRT_SulfuricCore(
                burstCenter,
                Main.rand.NextFloat(0.2f, 0.5f),
                90
            );
            PRTLoader.AddParticle(burstCore);

            //外圈快速扩散的毒液飞溅
            int burstCount = 24;
            for (int i = 0; i < burstCount; i++) {
                float angle = MathHelper.TwoPi * i / burstCount + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                PRT_AcidSplash burstSplash = new PRT_AcidSplash(
                    burstCenter + Main.rand.NextVector2Circular(10f, 10f),
                    velocity,
                    Main.rand.NextFloat(0.2f, 0.4f),
                    Main.rand.Next(6, 11),
                    true //受重力影响
                );
                PRTLoader.AddParticle(burstSplash);
            }

            //多层扩散波纹
            for (int i = 0; i < 3; i++) {
                PRT_CorrosionWave burstWave = new PRT_CorrosionWave(
                    burstCenter,
                    0.5f + i * 0.3f,
                    2f + i * 0.6f,
                    80 - i * 10,
                    Main.rand.NextFloat(MathHelper.TwoPi)
                );
                PRTLoader.AddParticle(burstWave);
            }

            //内圈酸雾扩散
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                PRT_ToxicMist mistBurst = new PRT_ToxicMist(
                    burstCenter + Main.rand.NextVector2Circular(15f, 15f),
                    velocity,
                    Main.rand.NextFloat(2f, 4f),
                    Main.rand.Next(10, 16),
                    Main.rand.NextFloat(0.6f, 1f)
                );
                PRTLoader.AddParticle(mistBurst);
            }

            //细小的腐蚀碎片
            for (int i = 0; i < 30; i++) {
                Vector2 fragmentVelocity = Main.rand.NextVector2Circular(6f, 6f);

                PRT_AcidSplash fragment = new PRT_AcidSplash(
                    burstCenter + Main.rand.NextVector2Circular(20f, 20f),
                    fragmentVelocity,
                    Main.rand.NextFloat(0.5f, 1f),
                    Main.rand.Next(50, 100),
                    true
                );
                PRTLoader.AddParticle(fragment);
            }

            //音效：硫酸沸腾声
            SoundEngine.PlaySound(SoundID.Item95 with { 
                Volume = 0.5f, 
                Pitch = -0.3f,
                MaxInstances = 2
            }, burstCenter);
            
            //额外的爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with { 
                Volume = 0.4f, 
                Pitch = -0.6f,
                MaxInstances = 2
            }, burstCenter);
        }

        public override void Unload() {
            IsActive = false;
            ActiveTimer = 0;
        }
    }
}
