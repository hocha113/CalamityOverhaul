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
        //锅的世界坐标位置
        private static Vector2 potWorldPosition;
        private static bool potPositionSet;

        //锅的动画参数
        private float potGlowTimer;
        private float bubbleTimer;
        private float steamTimer;

        //粒子系统
        private readonly List<SteamParticlePRT> steamParticles = [];
        private readonly List<BubbleParticlePRT> bubbleParticles = [];
        private int steamSpawnTimer;
        private int bubbleSpawnTimer;

        /// <summary>
        /// 设置锅的位置
        /// 在营地生成时自动调用
        /// </summary>
        public static void SetupPotPosition(Vector2 campsiteCenter) {
            if (potPositionSet) {
                return;
            }

            //在营地中心偏左下方寻找合适位置
            Vector2 searchOffset = new Vector2(-80f, 40f);
            Vector2 searchPos = campsiteCenter + searchOffset;

            //转换为图格坐标
            int tileX = (int)(searchPos.X / 16f);
            int tileY = (int)(searchPos.Y / 16f);

            //向下搜索最近的实心地面
            for (int y = tileY; y < tileY + 20; y++) {
                if (y < 0 || y >= Main.maxTilesY) {
                    continue;
                }

                Tile tile = Main.tile[tileX, y];
                if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                    //找到地面，设置锅的位置
                    potWorldPosition = new Vector2(tileX * 16f + 8f, y * 16f - 16f);
                    potPositionSet = true;
                    break;
                }
            }

            //如果没找到合适位置，使用默认偏移
            if (!potPositionSet) {
                potWorldPosition = searchPos;
                potPositionSet = true;
            }
        }

        /// <summary>
        /// 重置装饰状态
        /// 在营地清除时调用
        /// </summary>
        public static void ResetDecoration() {
            potPositionSet = false;
            potWorldPosition = Vector2.Zero;
        }

        public override void UpdateBySystem(int index) {
            if (!OldDukeCampsite.IsGenerated || !potPositionSet) {
                return;
            }

            //更新动画计时器
            potGlowTimer += 0.025f;
            bubbleTimer += 0.035f;
            steamTimer += 0.028f;

            if (potGlowTimer > MathHelper.TwoPi) potGlowTimer -= MathHelper.TwoPi;
            if (bubbleTimer > MathHelper.TwoPi) bubbleTimer -= MathHelper.TwoPi;
            if (steamTimer > MathHelper.TwoPi) steamTimer -= MathHelper.TwoPi;

            //生成蒸汽粒子
            steamSpawnTimer++;
            if (steamSpawnTimer >= 8 && steamParticles.Count < 15) {
                steamSpawnTimer = 0;
                SpawnSteamParticle();
            }

            //生成气泡粒子
            bubbleSpawnTimer++;
            if (bubbleSpawnTimer >= 12 && bubbleParticles.Count < 8) {
                bubbleSpawnTimer = 0;
                SpawnBubbleParticle();
            }

            //更新粒子
            for (int i = steamParticles.Count - 1; i >= 0; i--) {
                if (steamParticles[i].Update()) {
                    steamParticles.RemoveAt(i);
                }
            }

            for (int i = bubbleParticles.Count - 1; i >= 0; i--) {
                if (bubbleParticles[i].Update()) {
                    bubbleParticles.RemoveAt(i);
                }
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!OldDukeCampsite.IsGenerated || !potPositionSet) {
                return;
            }

            Vector2 screenPos = potWorldPosition - Main.screenPosition;

            //检查是否在屏幕范围内
            if (!VaultUtils.IsPointOnScreen(screenPos, 200)) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //先绘制粒子背景层
            foreach (var particle in bubbleParticles) {
                particle.Draw(spriteBatch);
            }

            //绘制锅
            DrawPot(spriteBatch, screenPos);

            //绘制蒸汽粒子前景层
            foreach (var particle in steamParticles) {
                particle.Draw(spriteBatch);
            }

            Main.spriteBatch.End();
        }

        /// <summary>
        /// 绘制锅
        /// </summary>
        private void DrawPot(SpriteBatch sb, Vector2 screenPos) {
            if (OldDukeCampsite.OldPot == null) {
                return;
            }

            Texture2D potTexture = OldDukeCampsite.OldPot;
            Vector2 origin = potTexture.Size() / 2f;

            //底部发光效果，模拟火光
            float glowIntensity = (MathF.Sin(potGlowTimer * 3f) * 0.5f + 0.5f) * 0.6f;
            Color fireGlow = new Color(255, 120, 60) with { A = 0 };

            //绘制火光层
            for (int i = 0; i < 3; i++) {
                float glowScale = 1.1f + i * 0.08f;
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                Vector2 glowOffset = new Vector2(0, 12f + i * 2f);

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
            DrawHeatWave(sb, screenPos);
        }

        /// <summary>
        /// 绘制热气波动效果
        /// </summary>
        private void DrawHeatWave(SpriteBatch sb, Vector2 screenPos) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //在锅上方绘制扭曲的热浪线条
            for (int i = 0; i < 3; i++) {
                float t = i / 3f;
                float yOffset = -20f - i * 8f;
                float wavePhase = steamTimer + t * MathHelper.Pi;
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
        private void SpawnSteamParticle() {
            Vector2 spawnPos = potWorldPosition;
            //在锅的上方随机位置生成
            spawnPos += new Vector2(
                Main.rand.NextFloat(-12f, 12f),
                -24f + Main.rand.NextFloat(-4f, 4f)
            );

            steamParticles.Add(new SteamParticlePRT(spawnPos));
        }

        /// <summary>
        /// 生成气泡粒子
        /// </summary>
        private void SpawnBubbleParticle() {
            Vector2 spawnPos = potWorldPosition;
            //在锅内部生成气泡
            spawnPos += new Vector2(
                Main.rand.NextFloat(-10f, 10f),
                Main.rand.NextFloat(-8f, 0f)
            );

            bubbleParticles.Add(new BubbleParticlePRT(spawnPos));
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

                //白色到淡黄色的蒸汽
                Color = Main.rand.NextBool() 
                    ? new Color(240, 240, 240, 180)
                    : new Color(255, 250, 230, 200);
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
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float alpha = MathF.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                //绘制蒸汽云团
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.5f),
                    Rotation,
                    new Vector2(0.5f),
                    Scale * 12f,
                    SpriteEffects.None,
                    0f
                );

                //中心高光
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.7f),
                    Rotation,
                    new Vector2(0.5f),
                    Scale * 6f,
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
