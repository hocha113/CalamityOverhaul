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

        //树木类型枚举(用于决定绘制风格)
        private TreeVisualType treeVisualType;
        //随机种子，用于生成一致的随机效果
        private int randomSeed;
        //树枝数据
        private BranchData[] branches;
        //叶片簇数据
        private LeafClusterData[] leafClusters;

        private enum TreeVisualType
        {
            Normal,     //普通树
            Palm,       //棕榈树
            Sakura,     //樱花树
            Willow,     //柳树
            Ash,        //灰烬树
            Mushroom    //蘑菇
        }

        private struct BranchData
        {
            public float Height;        //分支高度比例
            public float Angle;         //分支角度
            public float Length;        //分支长度
            public int Direction;       //方向(1右 -1左)
        }

        private struct LeafClusterData
        {
            public Vector2 Offset;      //相对偏移
            public float Size;          //大小
            public float Phase;         //动画相位
        }

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

            //根据原始树木类型决定视觉风格
            treeVisualType = originalTreeType switch {
                TileID.PalmTree => TreeVisualType.Palm,
                TileID.VanityTreeSakura => TreeVisualType.Sakura,
                TileID.VanityTreeYellowWillow => TreeVisualType.Willow,
                TileID.TreeAsh => TreeVisualType.Ash,
                _ => TreeVisualType.Normal
            };

            //检查地面是否是蘑菇草
            if (WorldGen.InWorld(targetTileX, targetTileY)) {
                Tile groundTile = Main.tile[targetTileX, targetTileY];
                if (groundTile.HasTile && groundTile.TileType == TileID.MushroomGrass) {
                    treeVisualType = TreeVisualType.Mushroom;
                }
            }

            //生成随机种子
            randomSeed = targetTileX * 1000 + targetTileY;

            //初始化树枝数据
            InitializeBranches();

            //初始化叶片簇数据
            InitializeLeafClusters();
        }

        private void InitializeBranches() {
            Random rand = new Random(randomSeed);
            int branchCount = treeVisualType switch {
                TreeVisualType.Palm => 0,
                TreeVisualType.Willow => 8,
                TreeVisualType.Mushroom => 0,
                _ => rand.Next(4, 7)
            };

            branches = new BranchData[branchCount];
            for (int i = 0; i < branchCount; i++) {
                branches[i] = new BranchData {
                    Height = 0.3f + (float)rand.NextDouble() * 0.5f,
                    Angle = MathHelper.ToRadians(20f + (float)rand.NextDouble() * 40f),
                    Length = 15f + (float)rand.NextDouble() * 25f,
                    Direction = rand.Next(2) == 0 ? -1 : 1
                };
            }
        }

        private void InitializeLeafClusters() {
            Random rand = new Random(randomSeed + 1);
            int clusterCount = treeVisualType switch {
                TreeVisualType.Palm => 8,
                TreeVisualType.Sakura => 12,
                TreeVisualType.Willow => 15,
                TreeVisualType.Mushroom => 1,
                TreeVisualType.Ash => 6,
                _ => rand.Next(8, 12)
            };

            leafClusters = new LeafClusterData[clusterCount];
            for (int i = 0; i < clusterCount; i++) {
                float angle = MathHelper.TwoPi * i / clusterCount + (float)rand.NextDouble() * 0.5f;
                float dist = 20f + (float)rand.NextDouble() * 30f;

                leafClusters[i] = new LeafClusterData {
                    Offset = new Vector2((float)Math.Cos(angle) * dist, (float)Math.Sin(angle) * dist * 0.6f - 20f),
                    Size = 0.8f + (float)rand.NextDouble() * 0.4f,
                    Phase = (float)rand.NextDouble() * MathHelper.TwoPi
                };
            }
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
            progress = VaultUtils.EaseOutQuad(progress);

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

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (growthPhase != 1) return false;

            Vector2 basePos = Center - Main.screenPosition;
            float progress = visualHeight / MaxVisualHeight;
            float time = Main.GlobalTimeWrappedHourly;

            //根据树木类型绘制不同风格
            switch (treeVisualType) {
                case TreeVisualType.Palm:
                    DrawPalmTree(spriteBatch, basePos, progress, time);
                    break;
                case TreeVisualType.Sakura:
                    DrawSakuraTree(spriteBatch, basePos, progress, time);
                    break;
                case TreeVisualType.Willow:
                    DrawWillowTree(spriteBatch, basePos, progress, time);
                    break;
                case TreeVisualType.Ash:
                    DrawAshTree(spriteBatch, basePos, progress, time);
                    break;
                case TreeVisualType.Mushroom:
                    DrawMushroom(spriteBatch, basePos, progress, time);
                    break;
                default:
                    DrawNormalTree(spriteBatch, basePos, progress, time);
                    break;
            }

            //绘制生长光效
            DrawGrowthGlow(spriteBatch, basePos, progress, time);

            return false;
        }

        /// <summary>
        /// 绘制普通树木
        /// </summary>
        private void DrawNormalTree(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float currentHeight = visualHeight;

            //绘制树根
            DrawTreeRoots(spriteBatch, basePos, progress, new Color(101, 67, 33));

            //绘制树干(带有纹理感)
            DrawTrunk(spriteBatch, basePos, currentHeight, progress, new Color(139, 90, 43), new Color(101, 67, 33));

            //绘制树枝
            foreach (var branch in branches) {
                if (progress > branch.Height) {
                    float branchProgress = (progress - branch.Height) / (1f - branch.Height);
                    branchProgress = Math.Min(branchProgress * 2f, 1f);
                    DrawBranch(spriteBatch, basePos, branch, currentHeight, branchProgress, new Color(120, 80, 40));
                }
            }

            //绘制树冠
            if (progress > 0.25f) {
                float crownProgress = (progress - 0.25f) / 0.75f;
                DrawLeafCrown(spriteBatch, basePos, currentHeight, crownProgress, time,
                    new Color(34, 139, 34), new Color(50, 205, 50), new Color(0, 100, 0));
            }
        }

        /// <summary>
        /// 绘制棕榈树
        /// </summary>
        private void DrawPalmTree(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            float currentHeight = visualHeight;

            //棕榈树干(弯曲的)
            DrawPalmTrunk(spriteBatch, basePos, currentHeight, progress, new Color(160, 120, 80), new Color(120, 90, 60));

            //棕榈叶(顶部扇形展开)
            if (progress > 0.4f) {
                float leafProgress = (progress - 0.4f) / 0.6f;
                DrawPalmLeaves(spriteBatch, basePos, currentHeight, leafProgress, time);
            }
        }

        /// <summary>
        /// 绘制樱花树
        /// </summary>
        private void DrawSakuraTree(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            float currentHeight = visualHeight;

            DrawTreeRoots(spriteBatch, basePos, progress, new Color(80, 50, 40));
            DrawTrunk(spriteBatch, basePos, currentHeight, progress, new Color(120, 80, 60), new Color(80, 50, 40));

            foreach (var branch in branches) {
                if (progress > branch.Height) {
                    float branchProgress = (progress - branch.Height) / (1f - branch.Height);
                    branchProgress = Math.Min(branchProgress * 2f, 1f);
                    DrawBranch(spriteBatch, basePos, branch, currentHeight, branchProgress, new Color(100, 70, 50));
                }
            }

            //樱花花冠(粉色)
            if (progress > 0.3f) {
                float crownProgress = (progress - 0.3f) / 0.7f;
                DrawLeafCrown(spriteBatch, basePos, currentHeight, crownProgress, time,
                    new Color(255, 182, 193), new Color(255, 105, 180), new Color(255, 20, 147));

                //飘落的花瓣
                DrawFallingPetals(spriteBatch, basePos, currentHeight, crownProgress, time);
            }
        }

        /// <summary>
        /// 绘制柳树
        /// </summary>
        private void DrawWillowTree(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            float currentHeight = visualHeight;

            DrawTreeRoots(spriteBatch, basePos, progress, new Color(90, 60, 30));
            DrawTrunk(spriteBatch, basePos, currentHeight, progress, new Color(130, 100, 50), new Color(90, 70, 40));

            //柳枝(下垂的)
            if (progress > 0.35f) {
                float branchProgress = (progress - 0.35f) / 0.65f;
                DrawWillowBranches(spriteBatch, basePos, currentHeight, branchProgress, time);
            }
        }

        /// <summary>
        /// 绘制灰烬树
        /// </summary>
        private void DrawAshTree(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            float currentHeight = visualHeight;

            //灰烬树干(暗灰色，带有裂纹)
            DrawTrunk(spriteBatch, basePos, currentHeight, progress, new Color(60, 50, 50), new Color(40, 35, 35));

            foreach (var branch in branches) {
                if (progress > branch.Height) {
                    float branchProgress = (progress - branch.Height) / (1f - branch.Height);
                    branchProgress = Math.Min(branchProgress * 2f, 1f);
                    DrawBranch(spriteBatch, basePos, branch, currentHeight, branchProgress, new Color(50, 45, 45));
                }
            }

            //灰烬树冠(暗红色发光)
            if (progress > 0.3f) {
                float crownProgress = (progress - 0.3f) / 0.7f;
                DrawAshCrown(spriteBatch, basePos, currentHeight, crownProgress, time);
            }
        }

        /// <summary>
        /// 绘制蘑菇
        /// </summary>
        private void DrawMushroom(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float currentHeight = visualHeight * 0.6f;

            //蘑菇柄
            float stemWidth = 12f + progress * 8f;
            for (float h = 0; h < currentHeight; h += 3f) {
                float heightRatio = h / currentHeight;
                float width = stemWidth * (1f - heightRatio * 0.3f);

                Vector2 pos = basePos + new Vector2(0, -h);
                Color stemColor = Color.Lerp(new Color(200, 180, 160), new Color(180, 160, 140), heightRatio) * 0.7f * progress;

                Rectangle rect = new Rectangle((int)(pos.X - width / 2), (int)pos.Y - 2, (int)width, 4);
                spriteBatch.Draw(pixel, rect, stemColor);
            }

            //蘑菇盖
            if (progress > 0.3f) {
                float capProgress = (progress - 0.3f) / 0.7f;
                float capWidth = 60f * capProgress;
                float capHeight = 30f * capProgress;
                Vector2 capPos = basePos + new Vector2(0, -currentHeight);

                //蘑菇盖渐变
                for (int layer = 0; layer < 8; layer++) {
                    float layerRatio = layer / 8f;
                    float layerWidth = capWidth * (1f - layerRatio * 0.5f);
                    float layerY = capPos.Y - capHeight * (1f - layerRatio);

                    Color capColor = Color.Lerp(new Color(70, 130, 180), new Color(100, 149, 237), layerRatio) * 0.6f * capProgress;

                    //添加发光斑点
                    float glowPulse = (float)Math.Sin(time * 3f + layer) * 0.3f + 0.7f;
                    if (layer % 2 == 0) {
                        capColor = Color.Lerp(capColor, new Color(150, 200, 255) * glowPulse, 0.3f);
                    }

                    Rectangle capRect = new Rectangle((int)(capPos.X - layerWidth / 2), (int)layerY, (int)layerWidth, (int)(capHeight / 8f) + 2);
                    spriteBatch.Draw(pixel, capRect, capColor);
                }
            }
        }

        #region 绘制辅助方法

        private void DrawTreeRoots(SpriteBatch spriteBatch, Vector2 basePos, float progress, Color rootColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (progress < 0.1f) return;

            float rootProgress = Math.Min(progress * 3f, 1f);
            Random rand = new Random(randomSeed + 100);

            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.ToRadians(-60f + i * 30f);
                float length = (10f + (float)rand.NextDouble() * 15f) * rootProgress;
                Vector2 rootEnd = basePos + new Vector2((float)Math.Cos(angle) * length, (float)Math.Sin(angle) * length * 0.5f + 5f);

                DrawLine(spriteBatch, pixel, basePos + new Vector2(0, 3), rootEnd, rootColor * 0.5f * rootProgress, 3f);
            }
        }

        private void DrawTrunk(SpriteBatch spriteBatch, Vector2 basePos, float currentHeight, float progress, Color trunkColor, Color darkColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float baseWidth = 10f;

            for (float h = 0; h < currentHeight; h += 3f) {
                float heightRatio = h / MaxVisualHeight;
                float width = baseWidth * (1f - heightRatio * 0.4f);

                //添加轻微的弯曲
                float sway = (float)Math.Sin(heightRatio * MathHelper.Pi * 2f + randomSeed) * 3f * heightRatio;

                Vector2 pos = basePos + new Vector2(sway, -h);

                //树干纹理：交替深浅色
                Color segmentColor = ((int)(h / 8f) % 2 == 0) ? trunkColor : darkColor;
                segmentColor *= 0.6f * (1f - heightRatio * 0.3f);

                Rectangle rect = new Rectangle((int)(pos.X - width / 2), (int)pos.Y - 2, (int)width, 4);
                spriteBatch.Draw(pixel, rect, segmentColor);

                //添加树皮纹理
                if ((int)(h / 12f) % 3 == 0 && width > 4f) {
                    Rectangle barkRect = new Rectangle((int)(pos.X - width / 4), (int)pos.Y - 1, (int)(width / 2), 2);
                    spriteBatch.Draw(pixel, barkRect, darkColor * 0.3f);
                }
            }
        }

        private void DrawPalmTrunk(SpriteBatch spriteBatch, Vector2 basePos, float currentHeight, float progress, Color trunkColor, Color darkColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float baseWidth = 8f;

            for (float h = 0; h < currentHeight; h += 4f) {
                float heightRatio = h / MaxVisualHeight;
                float width = baseWidth * (1f - heightRatio * 0.5f);

                //棕榈树特有的弯曲
                float curve = (float)Math.Sin(heightRatio * MathHelper.PiOver2) * 20f;
                Vector2 pos = basePos + new Vector2(curve, -h);

                //环状纹理
                Color segmentColor = ((int)(h / 6f) % 2 == 0) ? trunkColor : darkColor;
                segmentColor *= 0.7f;

                Rectangle rect = new Rectangle((int)(pos.X - width / 2), (int)pos.Y - 2, (int)width, 5);
                spriteBatch.Draw(pixel, rect, segmentColor);
            }
        }

        private void DrawBranch(SpriteBatch spriteBatch, Vector2 basePos, BranchData branch, float treeHeight, float branchProgress, Color branchColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float branchY = basePos.Y - treeHeight * branch.Height;
            float sway = (float)Math.Sin(branch.Height * MathHelper.Pi * 2f + randomSeed) * 3f * branch.Height;
            Vector2 branchStart = new Vector2(basePos.X + sway, branchY);

            float actualLength = branch.Length * branchProgress;
            float angle = branch.Direction > 0 ? -branch.Angle : (MathHelper.Pi + branch.Angle);

            Vector2 branchEnd = branchStart + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * actualLength;

            DrawLine(spriteBatch, pixel, branchStart, branchEnd, branchColor * 0.5f * branchProgress, 3f);

            //分支末端的小枝
            if (branchProgress > 0.5f) {
                Vector2 twigEnd = branchEnd + new Vector2((float)Math.Cos(angle - 0.3f * branch.Direction), (float)Math.Sin(angle - 0.3f * branch.Direction)) * 8f;
                DrawLine(spriteBatch, pixel, branchEnd, twigEnd, branchColor * 0.4f * branchProgress, 2f);
            }
        }

        private void DrawLeafCrown(SpriteBatch spriteBatch, Vector2 basePos, float treeHeight, float crownProgress, float time,
            Color leafColor1, Color leafColor2, Color leafColor3) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Vector2 crownCenter = basePos + new Vector2(0, -treeHeight + 20f);
            float maxSize = 50f * crownProgress;

            foreach (var cluster in leafClusters) {
                float swayX = (float)Math.Sin(time * 2f + cluster.Phase) * 3f;
                float swayY = (float)Math.Cos(time * 1.5f + cluster.Phase) * 2f;

                Vector2 clusterPos = crownCenter + cluster.Offset * crownProgress + new Vector2(swayX, swayY);
                float clusterSize = maxSize * cluster.Size * 0.6f;

                //多层叶片
                for (int layer = 0; layer < 3; layer++) {
                    float layerSize = clusterSize * (1f - layer * 0.25f);
                    Color layerColor = layer switch {
                        0 => leafColor3,
                        1 => leafColor1,
                        _ => leafColor2
                    };
                    layerColor *= 0.4f * crownProgress;

                    Rectangle leafRect = new Rectangle(
                        (int)(clusterPos.X - layerSize / 2),
                        (int)(clusterPos.Y - layerSize / 3 + layer * 5f),
                        (int)layerSize,
                        (int)(layerSize * 0.6f)
                    );
                    spriteBatch.Draw(pixel, leafRect, layerColor);
                }
            }
        }

        private void DrawPalmLeaves(SpriteBatch spriteBatch, Vector2 basePos, float treeHeight, float leafProgress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float curve = (float)Math.Sin(treeHeight / MaxVisualHeight * MathHelper.PiOver2) * 20f;
            Vector2 crownPos = basePos + new Vector2(curve, -treeHeight);

            int leafCount = 8;
            for (int i = 0; i < leafCount; i++) {
                float angle = MathHelper.TwoPi * i / leafCount - MathHelper.PiOver2;
                float leafLength = 40f * leafProgress;

                //叶片弯曲
                float droop = 0.3f + (float)Math.Sin(time * 2f + i) * 0.1f;

                for (float t = 0; t < 1f; t += 0.1f) {
                    float segmentAngle = angle + droop * t;
                    Vector2 segmentPos = crownPos + new Vector2(
                        (float)Math.Cos(segmentAngle) * leafLength * t,
                        (float)Math.Sin(segmentAngle) * leafLength * t + t * t * 15f
                    );

                    float segmentWidth = 6f * (1f - t * 0.7f);
                    Color leafColor = Color.Lerp(new Color(34, 139, 34), new Color(50, 205, 50), t) * 0.5f * leafProgress;

                    Rectangle rect = new Rectangle((int)(segmentPos.X - segmentWidth / 2), (int)segmentPos.Y - 2, (int)segmentWidth, 4);
                    spriteBatch.Draw(pixel, rect, leafColor);
                }
            }
        }

        private void DrawWillowBranches(SpriteBatch spriteBatch, Vector2 basePos, float treeHeight, float branchProgress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Vector2 crownPos = basePos + new Vector2(0, -treeHeight * 0.7f);

            Random rand = new Random(randomSeed + 200);
            int branchCount = 12;

            for (int i = 0; i < branchCount; i++) {
                float startAngle = MathHelper.TwoPi * i / branchCount + (float)rand.NextDouble() * 0.3f;
                float startDist = 10f + (float)rand.NextDouble() * 15f;
                Vector2 branchStart = crownPos + new Vector2((float)Math.Cos(startAngle), (float)Math.Sin(startAngle) * 0.3f - 0.5f) * startDist;

                float branchLength = 50f + (float)rand.NextDouble() * 40f;
                float sway = (float)Math.Sin(time * 1.5f + i * 0.5f) * 8f;

                //绘制下垂的柳枝
                for (float t = 0; t < 1f; t += 0.05f) {
                    float dropY = t * t * branchLength;
                    float swayX = sway * t;
                    Vector2 pos = branchStart + new Vector2(swayX, dropY) * branchProgress;

                    float width = 2f * (1f - t * 0.5f);
                    Color color = Color.Lerp(new Color(154, 205, 50), new Color(107, 142, 35), t) * 0.5f * branchProgress;

                    Rectangle rect = new Rectangle((int)(pos.X - width / 2), (int)pos.Y - 1, (int)width, 3);
                    spriteBatch.Draw(pixel, rect, color);
                }
            }
        }

        private void DrawAshCrown(SpriteBatch spriteBatch, Vector2 basePos, float treeHeight, float crownProgress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Vector2 crownCenter = basePos + new Vector2(0, -treeHeight + 15f);

            //发光的灰烬叶
            foreach (var cluster in leafClusters) {
                float pulse = (float)Math.Sin(time * 3f + cluster.Phase) * 0.3f + 0.7f;
                Vector2 clusterPos = crownCenter + cluster.Offset * crownProgress;
                float clusterSize = 30f * cluster.Size * crownProgress;

                //暗红色底层
                Color baseColor = new Color(80, 30, 30) * 0.5f * crownProgress;
                Rectangle baseRect = new Rectangle(
                    (int)(clusterPos.X - clusterSize / 2),
                    (int)(clusterPos.Y - clusterSize / 3),
                    (int)clusterSize,
                    (int)(clusterSize * 0.6f)
                );
                spriteBatch.Draw(pixel, baseRect, baseColor);

                //发光层
                Color glowColor = new Color(255, 100, 50) * 0.3f * pulse * crownProgress;
                Rectangle glowRect = new Rectangle(
                    (int)(clusterPos.X - clusterSize * 0.3f),
                    (int)(clusterPos.Y - clusterSize * 0.2f),
                    (int)(clusterSize * 0.6f),
                    (int)(clusterSize * 0.4f)
                );
                spriteBatch.Draw(pixel, glowRect, glowColor);
            }
        }

        private void DrawFallingPetals(SpriteBatch spriteBatch, Vector2 basePos, float treeHeight, float progress, float time) {
            if (progress < 0.5f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Random rand = new Random(randomSeed + (int)(time * 10f) % 100);

            int petalCount = (int)(8 * progress);
            for (int i = 0; i < petalCount; i++) {
                float fallProgress = ((time * 0.5f + i * 0.2f) % 1f);
                float x = basePos.X + (float)Math.Sin(time * 2f + i) * 40f + rand.Next(-30, 30);
                float y = basePos.Y - treeHeight + 50f + fallProgress * 150f;

                float rotation = time * 3f + i;
                float size = 3f + (float)Math.Sin(rotation) * 1f;

                Color petalColor = new Color(255, 182, 193) * 0.6f * (1f - fallProgress * 0.5f);
                Rectangle rect = new Rectangle((int)x, (int)y, (int)size, (int)size);
                spriteBatch.Draw(pixel, rect, petalColor);
            }
        }

        private void DrawGrowthGlow(SpriteBatch spriteBatch, Vector2 basePos, float progress, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float pulse = (float)Math.Sin(time * 4f) * 0.3f + 0.7f;
            float glowSize = 30f + progress * 50f;

            Color glowColor = treeVisualType switch {
                TreeVisualType.Sakura => new Color(255, 150, 200),
                TreeVisualType.Ash => new Color(255, 100, 50),
                TreeVisualType.Mushroom => new Color(100, 150, 255),
                _ => new Color(100, 255, 100)
            };
            glowColor *= 0.15f * pulse * progress;

            //底部发光
            Rectangle glowRect = new Rectangle(
                (int)(basePos.X - glowSize / 2),
                (int)(basePos.Y - 10),
                (int)glowSize,
                (int)(glowSize * 0.5f)
            );
            spriteBatch.Draw(pixel, glowRect, glowColor);
        }

        private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;

            float rotation = (float)Math.Atan2(diff.Y, diff.X);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
