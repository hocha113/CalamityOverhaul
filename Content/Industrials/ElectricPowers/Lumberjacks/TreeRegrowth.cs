using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    /// <summary>
    /// 树木生长动画Actor，用于表示树木快速重新生长的演出效果
    /// </summary>
    internal class TreeRegrowth : Actor
    {
        //目标种植位置(物块坐标)
        private int targetTileX;
        private int targetTileY;
        //原始树木类型
        private int originalTreeType;
        //生长计时器
        private int growthTimer;
        //生长阶段(0=准备 1=生长中 2=完成)
        private int growthPhase;
        //粒子生成计时器
        private int particleTimer;
        //视觉高度(用于绘制生长效果)
        private float visualHeight;
        //最大生长高度
        private const float MaxVisualHeight = 200f;
        //生长持续时间
        private const int GrowthDuration = 120;

        public override void OnSpawn(params object[] args) {
            Width = 32;
            Height = 32;
            DrawExtendMode = 300;
            DrawLayer = ActorDrawLayer.BeforeTiles;

            if (args is not null && args.Length >= 3) {
                targetTileX = (int)args[0];
                targetTileY = (int)args[1];
                originalTreeType = (int)args[2];
            }

            Position = new Vector2(targetTileX * 16, targetTileY * 16);
            growthTimer = 0;
            growthPhase = 0;
            particleTimer = 0;
            visualHeight = 0f;
        }

        public override void AI() {
            growthTimer++;
            particleTimer++;

            switch (growthPhase) {
                case 0:
                    //准备阶段，生成初始粒子效果
                    PhasePreparation();
                    break;
                case 1:
                    //生长阶段，持续生成粒子并增加视觉高度
                    PhaseGrowing();
                    break;
                case 2:
                    //完成阶段，实际种植树木
                    PhaseComplete();
                    break;
            }
        }

        private void PhasePreparation() {
            //播放开始生长音效
            if (growthTimer == 1) {
                SoundEngine.PlaySound(SoundID.Grass with {
                    Volume = 0.8f,
                    Pitch = 0.2f
                }, Center);
            }

            //生成准备粒子(向上冒出的绿色光点)
            if (particleTimer % 3 == 0 && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = Center + new Vector2(Main.rand.NextFloat(-16f, 16f), 0);
                    Vector2 dustVel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -4f));
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Grass, dustVel.X, dustVel.Y, 100, default, 1.5f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.2f;
                }
            }

            //30帧后进入生长阶段
            if (growthTimer >= 30) {
                growthTimer = 0;
                growthPhase = 1;
            }
        }

        private void PhaseGrowing() {
            //计算生长进度
            float progress = growthTimer / (float)GrowthDuration;
            progress = EaseOutQuad(progress);

            //更新视觉高度
            visualHeight = MaxVisualHeight * progress;

            //生成生长粒子效果
            if (particleTimer % 2 == 0 && Main.netMode != NetmodeID.Server) {
                //沿着生长高度生成粒子
                float spawnHeight = Main.rand.NextFloat(0, visualHeight);
                Vector2 dustPos = new Vector2(Center.X + Main.rand.NextFloat(-12f, 12f), Center.Y - spawnHeight);

                //绿色叶子粒子
                Dust leafDust = Dust.NewDustDirect(dustPos, 4, 4, DustID.GrassBlades, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f), 100, default, 1.2f);
                leafDust.noGravity = true;
                leafDust.fadeIn = 0.8f;

                //偶尔生成木屑粒子
                if (Main.rand.NextBool(3)) {
                    Dust woodDust = Dust.NewDustDirect(dustPos, 4, 4, DustID.WoodFurniture, Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f), 100, default, 0.8f);
                    woodDust.noGravity = true;
                }
            }

            //周期性播放生长音效
            if (growthTimer % 30 == 0) {
                SoundEngine.PlaySound(SoundID.Grass with {
                    Volume = 0.4f,
                    Pitch = 0.1f + progress * 0.3f
                }, Center);
            }

            //生长完成
            if (growthTimer >= GrowthDuration) {
                growthTimer = 0;
                growthPhase = 2;
            }
        }

        private void PhaseComplete() {
            //只在第一帧执行种植
            if (growthTimer == 1) {
                //实际种植树木
                PlantTree();

                //播放完成音效
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.6f,
                    Pitch = 0.5f
                }, Center);

                //生成完成粒子爆发
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 25; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(2f, 5f);
                        Vector2 dustVel = angle.ToRotationVector2() * speed;
                        Vector2 dustPos = Center + new Vector2(0, -visualHeight / 2) + Main.rand.NextVector2Circular(20f, visualHeight / 2);

                        Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.GrassBlades, dustVel.X, dustVel.Y, 100, default, 1.5f);
                        dust.noGravity = true;
                        dust.fadeIn = 1.5f;
                    }
                }
            }

            //短暂延迟后销毁
            if (growthTimer >= 20) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        /// <summary>
        /// 实际种植树木
        /// </summary>
        private void PlantTree() {
            if (VaultUtils.isClient) return;

            //在小范围内搜索最佳种植位置
            int bestX = -1;
            int bestY = -1;
            if (!FindBestPlantPosition(targetTileX, targetTileY, out bestX, out bestY)) {
                return;
            }

            Tile groundTile = Main.tile[bestX, bestY];
            int groundType = groundTile.TileType;
            bool success = false;

            //棕榈树需要沙地
            if (originalTreeType == TileID.PalmTree) {
                if (IsSandType(groundType)) {
                    success = WorldGen.GrowPalmTree(bestX, bestY);
                }
            }
            //灰烬树需要灰烬
            else if (originalTreeType == TileID.TreeAsh) {
                if (groundType == TileID.Ash) {
                    //清除上方可能存在的杂物
                    ClearAboveGround(bestX, bestY);
                    success = WorldGen.GrowTree(bestX, bestY - 1);
                }
            }
            //普通树木
            else {
                //清除上方可能存在的杂物
                ClearAboveGround(bestX, bestY);
                success = WorldGen.GrowTree(bestX, bestY - 1);
            }

            //如果第一次失败，尝试强制种树
            if (!success) {
                success = ForceGrowTree(bestX, bestY, groundType);
            }

            //同步物块变化
            if (success) {
                if (VaultUtils.isServer) {
                    NetMessage.SendTileSquare(-1, bestX, bestY - 20, 5, 30);
                }
            }
        }

        /// <summary>
        /// 在指定位置周围搜索最佳种植位置
        /// </summary>
        private static bool FindBestPlantPosition(int centerX, int centerY, out int bestX, out int bestY) {
            bestX = -1;
            bestY = -1;

            //先检查原始位置
            if (IsValidPlantPosition(centerX, centerY)) {
                bestX = centerX;
                bestY = centerY;
                return true;
            }

            //在3x3范围内搜索
            for (int offsetX = -1; offsetX <= 1; offsetX++) {
                for (int offsetY = -1; offsetY <= 1; offsetY++) {
                    if (offsetX == 0 && offsetY == 0) continue;

                    int checkX = centerX + offsetX;
                    int checkY = centerY + offsetY;

                    if (IsValidPlantPosition(checkX, checkY)) {
                        bestX = checkX;
                        bestY = checkY;
                        return true;
                    }
                }
            }

            //扩大到5x3范围搜索
            for (int offsetX = -2; offsetX <= 2; offsetX++) {
                for (int offsetY = -1; offsetY <= 1; offsetY++) {
                    int checkX = centerX + offsetX;
                    int checkY = centerY + offsetY;

                    if (IsValidPlantPosition(checkX, checkY)) {
                        bestX = checkX;
                        bestY = checkY;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检测指定位置是否是有效的种植位置
        /// </summary>
        private static bool IsValidPlantPosition(int x, int y) {
            if (!WorldGen.InWorld(x, y, 10)) return false;

            Tile groundTile = Main.tile[x, y];
            if (!groundTile.HasTile) return false;

            int groundType = groundTile.TileType;

            //检查是否是有效的地面类型
            if (!IsValidGroundType(groundType)) return false;

            //检查上方是否有足够空间(至少12格)
            for (int checkY = y - 1; checkY >= y - 12; checkY--) {
                if (!WorldGen.InWorld(x, checkY)) return false;
                Tile aboveTile = Main.tile[x, checkY];
                //允许草、花等非实心物块存在
                if (aboveTile.HasTile && Main.tileSolid[aboveTile.TileType] && !Main.tileSolidTop[aboveTile.TileType]) {
                    return false;
                }
            }

            //检查左右是否有足够空间(树木需要一定宽度)
            for (int checkX = x - 1; checkX <= x + 1; checkX++) {
                if (!WorldGen.InWorld(checkX, y - 1)) return false;
                Tile sideTile = Main.tile[checkX, y - 1];
                if (sideTile.HasTile && Main.tileSolid[sideTile.TileType]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 清除地面上方的杂物(草、花等)
        /// </summary>
        private static void ClearAboveGround(int x, int y) {
            for (int checkY = y - 1; checkY >= y - 3; checkY--) {
                if (!WorldGen.InWorld(x, checkY)) continue;
                Tile tile = Main.tile[x, checkY];
                if (tile.HasTile && !Main.tileSolid[tile.TileType]) {
                    //移除非实心物块(草、花、蘑菇等)
                    WorldGen.KillTile(x, checkY, false, false, true);
                }
            }
        }

        /// <summary>
        /// 强制种植树木
        /// </summary>
        private static bool ForceGrowTree(int x, int y, int groundType) {
            //根据地面类型尝试不同的种树方法
            if (IsSandType(groundType)) {
                //沙地种棕榈树
                return WorldGen.GrowPalmTree(x, y);
            }
            else if (groundType == TileID.Ash) {
                //灰烬地种灰烬树
                WorldGen.PlaceTile(x, y, TileID.Saplings, true, false, -1, 0);
                return WorldGen.GrowTree(x, y);
            }
            else if (groundType == TileID.JungleGrass) {
                //丛林草地
                return WorldGen.GrowTree(x, y);
            }
            else if (groundType == TileID.MushroomGrass) {
                //蘑菇草地种巨型蘑菇
                return WorldGen.GrowShroom(x, y);
            }
            else {
                //普通草地
                return WorldGen.GrowTree(x, y);
            }
        }

        /// <summary>
        /// 检查是否是有效的地面类型
        /// </summary>
        private static bool IsValidGroundType(int groundType) {
            return groundType == TileID.Grass ||
                   groundType == TileID.CorruptGrass ||
                   groundType == TileID.CrimsonGrass ||
                   groundType == TileID.HallowedGrass ||
                   groundType == TileID.JungleGrass ||
                   groundType == TileID.MushroomGrass ||
                   groundType == TileID.Sand ||
                   groundType == TileID.Crimsand ||
                   groundType == TileID.Ebonsand ||
                   groundType == TileID.Pearlsand ||
                   groundType == TileID.Ash;
        }

        /// <summary>
        /// 检查是否是沙地类型
        /// </summary>
        private static bool IsSandType(int groundType) {
            return groundType == TileID.Sand ||
                   groundType == TileID.Crimsand ||
                   groundType == TileID.Ebonsand ||
                   groundType == TileID.Pearlsand;
        }

        /// <summary>
        /// 检测是否可以在此位置种树(保留用于兼容)
        /// </summary>
        private static bool CanPlantTreeHere(int x, int y) {
            return IsValidPlantPosition(x, y);
        }

        /// <summary>
        /// 缓出二次方缓动函数
        /// </summary>
        private static float EaseOutQuad(float t) {
            return 1f - (1f - t) * (1f - t);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (growthPhase != 1) return false;

            //绘制生长中的视觉效果(半透明的树木轮廓)
            Vector2 basePos = Center - Main.screenPosition;

            //绘制树干轮廓
            float trunkWidth = 8f;
            float currentHeight = visualHeight;

            for (float h = 0; h < currentHeight; h += 4f) {
                float heightRatio = h / MaxVisualHeight;
                float widthAtHeight = trunkWidth * (1f - heightRatio * 0.3f);

                Vector2 segmentPos = basePos + new Vector2(0, -h);
                Color trunkColor = Color.SaddleBrown * 0.4f * (1f - heightRatio * 0.5f);

                //简单的矩形表示树干
                Rectangle rect = new Rectangle((int)(segmentPos.X - widthAtHeight / 2), (int)segmentPos.Y - 2, (int)widthAtHeight, 4);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, rect, trunkColor);
            }

            //绘制树冠轮廓
            if (currentHeight > MaxVisualHeight * 0.3f) {
                float crownProgress = (currentHeight - MaxVisualHeight * 0.3f) / (MaxVisualHeight * 0.7f);
                float crownSize = 40f * crownProgress;
                Vector2 crownPos = basePos + new Vector2(0, -currentHeight + crownSize * 0.3f);

                Color crownColor = Color.ForestGreen * 0.35f * crownProgress;

                //绘制多层树冠
                for (int layer = 0; layer < 3; layer++) {
                    float layerSize = crownSize * (1f - layer * 0.25f);
                    float layerOffset = layer * 15f;
                    Vector2 layerPos = crownPos + new Vector2(0, layerOffset);

                    Rectangle crownRect = new Rectangle(
                        (int)(layerPos.X - layerSize),
                        (int)(layerPos.Y - layerSize * 0.6f),
                        (int)(layerSize * 2),
                        (int)(layerSize * 1.2f)
                    );
                    spriteBatch.Draw(VaultAsset.placeholder2.Value, crownRect, crownColor);
                }
            }

            return false;
        }
    }
}
