using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地装饰渲染器
    /// 负责渲染营地中的装饰物品和环境效果
    /// </summary>
    internal class OldDukeCampsiteDecoration : RenderHandle
    {
        //多个锅的位置信息
        private class PotData
        {
            public Vector2 WorldPosition;
            public float GlowTimer;
            public float BubbleTimer;
            public float SteamTimer;
            public List<SteamParticlePRT> SteamParticles = [];
            public List<BubbleParticlePRT> BubbleParticles = [];
            public int SteamSpawnTimer;
            public int BubbleSpawnTimer;
        }

        private static readonly List<PotData> pots = [];
        private static bool potsPositionSet;

        /// <summary>
        /// 设置锅的位置
        /// 在营地生成时自动调用
        /// </summary>
        public static void SetupPotPosition(Vector2 campsiteCenter) {
            if (potsPositionSet) {
                return;
            }

            pots.Clear();

            //定义多个锅的相对偏移位置
            //主要布置在老公爵前方和两侧，避免被遮挡
            Vector2[] potOffsets = [
                new Vector2(220f, 40f),  //右前方
                new Vector2(-240f, 35f), //左前方
                new Vector2(280f, 50f),  //右侧远处
                new Vector2(-160f, 55f)   //左侧
            ];

            foreach (var offset in potOffsets) {
                Vector2 searchPos = campsiteCenter + offset;
                int tileX = (int)(searchPos.X / 16f);
                int tileY = (int)(searchPos.Y / 16f) - 60;

                Vector2 finalPos = searchPos;
                bool foundGround = false;

                //向下搜索最近的实心地面
                for (int y = tileY; y < tileY + 125; y++) {
                    if (y < 0 || y >= Main.maxTilesY) {
                        continue;
                    }

                    Tile tile = Main.tile[tileX, y];
                    if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                        finalPos = new Vector2(tileX * 16f + 8f, y * 16f - 16f);
                        foundGround = true;
                        break;
                    }
                }

                //如果找到地面或使用默认位置，添加这个锅
                if (foundGround || true) {
                    PotData pot = new PotData {
                        WorldPosition = finalPos,
                        GlowTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                        BubbleTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                        SteamTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi)
                    };
                    pots.Add(pot);
                }
            }

            potsPositionSet = true;
        }

        /// <summary>
        /// 重置装饰状态
        /// 在营地清除时调用
        /// </summary>
        public static void ResetDecoration() {
            potsPositionSet = false;
            pots.Clear();
        }

        public override void UpdateBySystem(int index) {
            if (!OldDukeCampsite.IsGenerated || !potsPositionSet) {
                return;
            }

            //更新每个锅的状态
            foreach (var pot in pots) {
                UpdatePot(pot);
            }
        }

        /// <summary>
        /// 更新单个锅的状态
        /// </summary>
        private void UpdatePot(PotData pot) {
            //更新动画计时器
            pot.GlowTimer += 0.025f;
            pot.BubbleTimer += 0.035f;
            pot.SteamTimer += 0.028f;

            if (pot.GlowTimer > MathHelper.TwoPi) pot.GlowTimer -= MathHelper.TwoPi;
            if (pot.BubbleTimer > MathHelper.TwoPi) pot.BubbleTimer -= MathHelper.TwoPi;
            if (pot.SteamTimer > MathHelper.TwoPi) pot.SteamTimer -= MathHelper.TwoPi;

            //生成蒸汽粒子
            pot.SteamSpawnTimer++;
            if (pot.SteamSpawnTimer >= 10 && pot.SteamParticles.Count < 12) {
                pot.SteamSpawnTimer = 0;
                SpawnSteamParticle(pot);
            }

            //生成气泡粒子
            pot.BubbleSpawnTimer++;
            if (pot.BubbleSpawnTimer >= 15 && pot.BubbleParticles.Count < 6) {
                pot.BubbleSpawnTimer = 0;
                SpawnBubbleParticle(pot);
            }

            //更新粒子
            for (int i = pot.SteamParticles.Count - 1; i >= 0; i--) {
                if (pot.SteamParticles[i].Update()) {
                    pot.SteamParticles.RemoveAt(i);
                }
            }

            for (int i = pot.BubbleParticles.Count - 1; i >= 0; i--) {
                if (pot.BubbleParticles[i].Update()) {
                    pot.BubbleParticles.RemoveAt(i);
                }
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!OldDukeCampsite.IsGenerated || !potsPositionSet) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制所有锅
            foreach (var pot in pots) {
                Vector2 screenPos = pot.WorldPosition - Main.screenPosition;

                //检查是否在屏幕范围内
                if (!VaultUtils.IsPointOnScreen(screenPos, 200)) {
                    continue;
                }

                //绘制气泡背景层
                foreach (var particle in pot.BubbleParticles) {
                    particle.Draw(spriteBatch);
                }

                //绘制锅
                DrawPot(spriteBatch, screenPos, pot);

                //绘制蒸汽前景层
                foreach (var particle in pot.SteamParticles) {
                    particle.Draw(spriteBatch);
                }
            }

            Main.spriteBatch.End();
        }

        /// <summary>
        /// 绘制锅
        /// </summary>
        private void DrawPot(SpriteBatch sb, Vector2 screenPos, PotData pot) {
            if (OldDukeCampsite.OldPot == null) {
                return;
            }

            Texture2D potTexture = OldDukeCampsite.OldPot;
            Vector2 origin = potTexture.Size() / 2f;

            //底部发光效果模拟火光
            float glowIntensity = (MathF.Sin(pot.GlowTimer * 3f) * 0.5f + 0.5f) * 0.6f;
            Color fireGlow = new Color(255, 120, 60) with { A = 0 };

            //绘制火光层
            for (int i = 0; i < 3; i++) {
                float glowScale = 1.1f + i * 0.08f;
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                Vector2 glowOffset = new Vector2(0, -6f + i * 2f);

                sb.Draw(
                    potTexture,
                    screenPos + glowOffset,
                    null,
                    fireGlow * glowAlpha,
                    0f,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //绘制锅主体
            sb.Draw(
                potTexture,
                screenPos,
                null,
                Color.White,
                0f,
                origin,
                1f,
                SpriteEffects.None,
                0f
            );

            //绘制热气波动效果
            DrawHeatWave(sb, screenPos, pot);
        }

        /// <summary>
        /// 绘制热气波动效果
        /// </summary>
        private void DrawHeatWave(SpriteBatch sb, Vector2 screenPos, PotData pot) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //在锅上方绘制扭曲的热浪线条
            for (int i = 0; i < 3; i++) {
                float t = i / 3f;
                float yOffset = -20f - i * 8f;
                float wavePhase = pot.SteamTimer + t * MathHelper.Pi;
                float xOffset = MathF.Sin(wavePhase) * 6f;

                Vector2 wavePos = screenPos + new Vector2(xOffset, yOffset);
                Color waveColor = new Color(255, 200, 150) * (0.15f * (1f - t * 0.5f));

                sb.Draw(
                    pixel,
                    wavePos,
                    new Rectangle(0, 0, 1, 1),
                    waveColor,
                    0f,
                    new Vector2(0.5f),
                    new Vector2(20f - i * 4f, 1.5f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 生成蒸汽粒子
        /// </summary>
        private void SpawnSteamParticle(PotData pot) {
            Vector2 spawnPos = pot.WorldPosition;
            //在锅的上方随机位置生成
            spawnPos += new Vector2(
                Main.rand.NextFloat(-12f, 12f),
                -24f + Main.rand.NextFloat(-4f, 4f)
            );

            pot.SteamParticles.Add(new SteamParticlePRT(spawnPos));
        }

        /// <summary>
        /// 生成气泡粒子
        /// </summary>
        private void SpawnBubbleParticle(PotData pot) {
            Vector2 spawnPos = pot.WorldPosition;
            //在锅内部生成气泡
            spawnPos += new Vector2(
                Main.rand.NextFloat(-10f, 10f),
                Main.rand.NextFloat(-8f, 0f)
            );

            pot.BubbleParticles.Add(new BubbleParticlePRT(spawnPos));
        }

        /// <summary>
        /// 蒸汽粒子
        /// 从锅中升起的热蒸汽效果
        /// </summary>
        private class SteamParticlePRT
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public Color Color;

            public SteamParticlePRT(Vector2 startPos) {
                Position = startPos;
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-1.5f, -0.8f)
                );
                Scale = Main.rand.NextFloat(0.4f, 0.8f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(45f, 75f);
                Rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f);

                //黄色到淡黄绿色的蒸汽
                Color = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Yellow, Color.YellowGreen);
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Rotation += RotationSpeed;

                //横向飘动
                Velocity.X += MathF.Sin(Life * 0.08f) * 0.03f;

                //向上减速
                Velocity.Y *= 0.98f;
                Velocity.X *= 0.99f;

                //逐渐扩散
                Scale += 0.008f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = CWRAsset.SoftGlow.Value;
                float alpha = MathF.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                //绘制蒸汽云团
                sb.Draw(
                    pixel,
                    screenPos,
                    null,
                    Color with { A = 0 } * (alpha * 0.5f),
                    Rotation,
                    pixel.Size() / 2,
                    Scale,
                    SpriteEffects.None,
                    0f
                );

                //中心高光
                sb.Draw(
                    pixel,
                    screenPos,
                    null,
                    Color with { A = 0 } * (alpha * 0.7f),
                    Rotation,
                    pixel.Size() / 2,
                    Scale / 2,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 气泡粒子
        /// 锅内沸腾的气泡效果
        /// </summary>
        private class BubbleParticlePRT
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Life;
            public float MaxLife;
            public Color Color;

            public BubbleParticlePRT(Vector2 startPos) {
                Position = startPos;
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.2f, 0.2f),
                    Main.rand.NextFloat(-0.8f, -0.4f)
                );
                Scale = Main.rand.NextFloat(0.3f, 0.6f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(20f, 35f);

                //淡绿色的毒液气泡
                Color = Main.rand.NextBool()
                    ? new Color(140, 200, 120, 200)
                    : new Color(160, 220, 140, 220);
            }

            public bool Update() {
                Life++;
                Position += Velocity;

                //上升时左右摆动
                Velocity.X += MathF.Sin(Life * 0.15f) * 0.01f;

                //速度衰减
                Velocity *= 0.98f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float alpha = MathF.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                //外圈
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.5f),
                    0f,
                    new Vector2(0.5f),
                    Scale * 6f,
                    SpriteEffects.None,
                    0f
                );

                //内核
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * alpha,
                    0f,
                    new Vector2(0.5f),
                    Scale * 3f,
                    SpriteEffects.None,
                    0f
                );

                //高光
                Vector2 highlightOffset = new Vector2(-Scale * 1.5f, -Scale * 1.5f);
                sb.Draw(
                    pixel,
                    screenPos + highlightOffset,
                    new Rectangle(0, 0, 1, 1),
                    new Color(255, 255, 255, 150) * alpha,
                    0f,
                    new Vector2(0.5f),
                    Scale * 1.5f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
