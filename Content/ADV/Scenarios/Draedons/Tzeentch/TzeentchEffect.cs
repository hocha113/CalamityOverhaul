using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch
{
    /// <summary>
    /// 奸奇场景效果
    /// </summary>
    internal class TzeentchSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => TzeentchEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(TzeentchSky.Name, isActive);
    }

    /// <summary>
    /// 奸奇魔法天空效果
    /// </summary>
    internal class TzeentchSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:TzeentchSky";
        private bool active;
        private float intensity;

        //魔法光环效果参数
        private float magicPulseTimer = 0f;
        private float colorShiftTimer = 0f;

        //迷雾效果参数
        private readonly MysticFog[] fogs = new MysticFog[88];
        
        //魔法符文闪烁效果
        private readonly MagicRune[] runes = new MagicRune[16];

        //颜色变换
        private Color[] tzeentchColors = new Color[]
        {
            new Color(138, 43, 226),   // 蓝紫色
            new Color(75, 0, 130),     // 靛蓝色
            new Color(255, 0, 255),    // 品红色
            new Color(0, 191, 255),    // 深天蓝
            new Color(138, 43, 226),   // 紫罗兰
            new Color(199, 21, 133),   // 中紫罗兰红
        };

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;

            //创建魔法紫色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.2f, 0.1f, 0.3f)//紫色魔法调
                .UseOpacity(0.5f), EffectPriority.High);

            //初始化迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i] = new MysticFog();
            }

            //初始化符文
            for (int i = 0; i < runes.Length; i++) {
                runes[i] = new MagicRune();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            magicPulseTimer = 0f;
            colorShiftTimer = 0f;

            //重置迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i].Reset();
            }

            //重置符文
            for (int i = 0; i < runes.Length; i++) {
                runes[i].Reset();
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            //深紫魔法背景
            Color bgColor = new Color(15, 8, 25);
            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                bgColor * intensity * 0.9f
            );

            //绘制魔法迷雾层
            DrawMysticFogs(spriteBatch);

            //绘制魔法符文
            DrawMagicRunes(spriteBatch);

            //绘制魔法脉冲效果
            DrawMagicPulse(spriteBatch);

            //绘制变幻色彩涟漪
            DrawColorRipples(spriteBatch);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            _ = TzeentchEffect.Cek();

            //强度变化
            if (TzeentchEffect.IsActive) {
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

            //更新魔法脉冲
            magicPulseTimer += 0.04f;
            if (magicPulseTimer > MathHelper.TwoPi) {
                magicPulseTimer -= MathHelper.TwoPi;
            }

            //更新颜色变换
            colorShiftTimer += 0.02f;
            if (colorShiftTimer > MathHelper.TwoPi * 2) {
                colorShiftTimer -= MathHelper.TwoPi * 2;
            }

            //更新迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i].Update();
            }

            //更新符文
            for (int i = 0; i < runes.Length; i++) {
                runes[i].Update();
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用魔法紫色调
            if (intensity > 0.1f) {
                float magicR = 0.9f;
                float magicG = 0.7f;
                float magicB = 1.0f;

                Color tintedColor = new Color(
                    (int)(inColor.R * magicR),
                    (int)(inColor.G * magicG),
                    (int)(inColor.B * magicB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.4f);
            }
            return inColor;
        }

        #region 绘制特效方法
        private void DrawMysticFogs(SpriteBatch sb) {
            //使用Fog纹理绘制迷雾
            if (CWRAsset.Fog == null || CWRAsset.Fog.IsDisposed) {
                return;
            }

            Texture2D fogTex = CWRAsset.Fog.Value;

            for (int i = 0; i < fogs.Length; i++) {
                MysticFog fog = fogs[i];
                if (!fog.IsActive) {
                    continue;
                }

                Vector2 drawPos = fog.Position - Main.screenPosition;
                
                //根据时间变换颜色
                int colorIndex = (int)(colorShiftTimer * 3f + i) % tzeentchColors.Length;
                Color fogColor = Color.Lerp(tzeentchColors[colorIndex], 
                    tzeentchColors[(colorIndex + 1) % tzeentchColors.Length], 
                    (float)Math.Sin(colorShiftTimer * 2f + i) * 0.5f + 0.5f);

                float alpha = (float)Math.Sin(fog.AnimProgress * MathHelper.Pi) * intensity * 0.3f;

                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha,
                    fog.Rotation,
                    fogTex.Size() * 0.5f,
                    fog.Scale,
                    SpriteEffects.None,
                    0f
                );

                //添加发光层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha * 0.5f,
                    fog.Rotation * 0.8f,
                    fogTex.Size() * 0.5f,
                    fog.Scale * 1.3f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawMagicRunes(SpriteBatch sb) {
            //使用TileHightlight纹理绘制魔法符文
            if (CWRAsset.TileHightlight == null || CWRAsset.TileHightlight.IsDisposed) {
                return;
            }

            Texture2D runeTex = CWRAsset.TileHightlight.Value;
            int frameWidth = runeTex.Width / 3;
            int frameHeight = runeTex.Height / 3;

            for (int i = 0; i < runes.Length; i++) {
                MagicRune rune = runes[i];
                if (!rune.IsActive) {
                    continue;
                }

                int currentFrame = (int)(rune.AnimProgress * 9);
                if (currentFrame >= 9) {
                    currentFrame = 8;
                }

                int frameX = (currentFrame % 3) * frameWidth;
                int frameY = (currentFrame / 3) * frameHeight;
                Rectangle sourceRect = new Rectangle(frameX, frameY, frameWidth, frameHeight);

                Vector2 drawPos = rune.Position - Main.screenPosition;
                
                //变幻的颜色
                int colorIndex = (int)(colorShiftTimer * 2f + i * 1.5f) % tzeentchColors.Length;
                Color runeColor = tzeentchColors[colorIndex];

                float alpha = (float)Math.Sin(rune.AnimProgress * MathHelper.Pi) * intensity * 0.8f;
                float scale = rune.Scale * (1f + (float)Math.Sin(rune.AnimProgress * MathHelper.Pi * 2f) * 0.2f);

                for (int z = 0; z < 16; z++) {
                    sb.Draw(
                    runeTex,
                    drawPos,
                    sourceRect,
                    runeColor with { A = 0 } * alpha,
                    rune.Rotation + z * 0.1f,
                    new Vector2(frameWidth, frameHeight) * 0.5f,
                    scale + z * 0.21f,
                    SpriteEffects.None,
                    0f
                    );

                    //额外的魔法光晕
                    sb.Draw(
                        runeTex,
                        drawPos,
                        sourceRect,
                        runeColor with { A = 0 } * alpha * 0.4f,
                        -rune.Rotation * 0.5f + z * 0.1f,
                        new Vector2(frameWidth, frameHeight) * 0.5f,
                        scale * 1.5f + z * 0.1f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawMagicPulse(SpriteBatch sb) {
            //绘制从中心向外的魔法脉冲
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            float pulseAlpha = (float)Math.Sin(magicPulseTimer * 1.5f) * 0.5f + 0.5f;
            int colorIndex = (int)(colorShiftTimer * 1.5f) % tzeentchColors.Length;
            Color pulseColor = Color.Lerp(tzeentchColors[colorIndex], 
                tzeentchColors[(colorIndex + 1) % tzeentchColors.Length], 
                pulseAlpha);

            //中心脉冲
            Vector2 centerPos = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float radius = (float)Math.Sin(magicPulseTimer) * 100f + 200f;
            
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + magicPulseTimer;
                Vector2 offset = angle.ToRotationVector2() * radius;
                
                sb.Draw(pixel,
                    centerPos + offset,
                    new Rectangle(0, 0, 3, 3),
                    pulseColor * (pulseAlpha * intensity * 0.4f));
            }
        }

        private void DrawColorRipples(SpriteBatch sb) {
            //绘制变幻色彩涟漪效果
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            int rippleCount = 3;
            for (int i = 0; i < rippleCount; i++) {
                float phase = (colorShiftTimer + i * MathHelper.TwoPi / rippleCount) % MathHelper.TwoPi;
                float rippleAlpha = (float)Math.Sin(phase) * 0.5f + 0.5f;
                
                int colorIndex = (int)(phase * 3f) % tzeentchColors.Length;
                Color rippleColor = tzeentchColors[colorIndex];

                //水平涟漪
                int y = (int)(Main.screenHeight * (0.3f + i * 0.2f));
                sb.Draw(pixel,
                    new Rectangle(0, y, Main.screenWidth, 2),
                    rippleColor * (rippleAlpha * intensity * 0.15f));
            }
        }
        #endregion

        #region 迷雾类
        private class MysticFog
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public MysticFog() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(30, 120);
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
                Rotation += 0.002f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.008f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-200, Main.screenWidth + 200),
                    Main.screenPosition.Y + Main.rand.Next(-200, Main.screenHeight + 200)
                );

                Velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
                Scale = Main.rand.NextFloat(2f, 4f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 魔法符文类
        private class MagicRune
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;

            private int cooldown;

            public MagicRune() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(60, 200);
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
                Rotation += 0.01f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.01f, 0.02f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(100, Main.screenWidth - 100),
                    Main.screenPosition.Y + Main.rand.Next(100, Main.screenHeight - 100)
                );

                Scale = Main.rand.NextFloat(1.2f, 2.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    /// <summary>
    /// 奸奇场景效果管理器
    /// </summary>
    internal class TzeentchEffect : ModSystem
    {
        public static bool IsActive;
        public static int CekTimer = 0;
        private int particleTimer = 0;
        private int magicBurstTimer = 0;

        //奸奇特有颜色
        private static readonly Color[] TzeentchColors =
        [
            new Color(138, 43, 226),   // 蓝紫色
            new Color(75, 0, 130),     // 靛蓝色
            new Color(255, 0, 255),    // 品红色
            new Color(0, 191, 255),    // 深天蓝
        ];

        internal static void Send() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.TzeentchEffect);
            packet.Write(IsActive);
            packet.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.TzeentchEffect) {
                IsActive = reader.ReadBoolean();
                if (VaultUtils.isServer) {
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.TzeentchEffect);
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
            magicBurstTimer++;

            //生成烟雾粒子
            if (particleTimer % 2 == 0) {
                SpawnMysticSmoke();
            }

            Main.newMusic = Main.musicBox2 = -1;
        }

        private static void SpawnMysticSmoke() {
            //生成神秘烟雾
            Vector2 spawnPos = new Vector2(
                Main.screenPosition.X + Main.rand.Next(Main.screenWidth),
                Main.screenPosition.Y + Main.rand.Next(Main.screenHeight)
            );

            Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f);
            Color smokeColor = TzeentchColors[Main.rand.Next(TzeentchColors.Length)];

            PRT_Smoke mysticSmoke = new PRT_Smoke(
                spawnPos,
                velocity,
                smokeColor,
                Main.rand.Next(120, 200),
                Main.rand.NextFloat(0.8f, 1.5f),
                0.6f,
                Main.rand.NextFloat(-0.02f, 0.02f),
                glowing: true,
                hueshift: 0.01f
            );
            PRTLoader.AddParticle(mysticSmoke);
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
