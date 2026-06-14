using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines
{
    internal class MachineGen : GenPass
    {
        //安全边距，防止PlaceTile和KillTile访问相邻格子时越界
        private const int SafePadding = 10;

        public MachineGen() : base("Machine World", 110) { }

        //检查坐标是否在世界安全范围内，预留边距防止越界
        private static bool InWorldSafe(int x, int y) {
            return x >= SafePadding && x < Main.maxTilesX - SafePadding
                && y >= SafePadding && y < Main.maxTilesY - SafePadding;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            int typeDark = ModContent.TileType<MechanicalDark>();
            int typeStone = ModContent.TileType<MechanicalStone>();
            int[] mechTiles = [typeDark, typeStone, TileID.AdamantiteBeam, TileID.Cog, TileID.LihzahrdBrick];

            GenerationProgress cache = WorldGenerator.CurrentGenerationProgress;

            //先生成一个正常的原版世界作为基底，然后在上面进行机械风格替换
            try {
                WorldGen.GenerateWorld(Main.ActiveWorldFileData.Seed);
            } catch {
                //原版生成中某些非关键步骤可能抛出异常，忽略后继续机械化改造
            }

            int worldWidth = Main.maxTilesX;
            int worldHeight = Main.maxTilesY;
            int surfaceY = Math.Clamp((int)Main.worldSurface, SafePadding + 30, worldHeight - SafePadding);
            int spawnX = Math.Clamp(Main.spawnTileX, SafePadding, worldWidth - SafePadding);

            //替换地表和地下已有物块为机械风格
            ReplaceTilesToMechanical(worldWidth, worldHeight, typeDark, mechTiles);

            //用多层噪声生成连续的地表轮廓线
            int[] surfaceHeightMap = BuildSurfaceHeightMap(worldWidth, worldHeight, surfaceY, spawnX);

            //沿轮廓线铺设地表物块
            LayMechanicalSurface(worldWidth, worldHeight, surfaceHeightMap, typeDark, typeStone, mechTiles);

            //在轮廓线上稀疏地生成尖塔结构
            GenerateSpires(worldWidth, worldHeight, surfaceHeightMap, typeStone, spawnX);

            //处理玩家出生点，清理出平坦的着陆区
            ClearSpawnArea(worldWidth, worldHeight, surfaceHeightMap, spawnX);

            //沿实际地表放置机械装饰
            PlaceSurfaceDecorations(worldWidth, worldHeight, surfaceHeightMap);

            //将地表线和岩石层推到世界最底部，防止游戏渲染地下背景墙
            Main.worldSurface = worldHeight - 2;
            Main.rockLayer = worldHeight - 1;

            //清除原版世界生成过程中产生的所有NPC（如向导）
            for (int i = 0; i < Main.maxNPCs; i++) {
                Main.npc[i] = new NPC();
            }

            WorldGenerator.CurrentGenerationProgress = cache;
        }

        /// <summary>

        /// 将原版世界中所有已有物块替换为机械风格

        /// </summary>
        private static void ReplaceTilesToMechanical(int worldWidth, int worldHeight, int typeDark, int[] mechTiles) {
            for (int x = SafePadding; x < worldWidth - SafePadding; x++) {
                for (int y = SafePadding; y < worldHeight - SafePadding; y++) {
                    Tile tile = Main.tile[x, y];

                    //清除所有原版墙壁
                    if (tile.WallType != WallID.None) {
                        tile.WallType = WallID.None;
                    }

                    //清除液体
                    if (tile.LiquidAmount > 0) {
                        tile.LiquidAmount = 0;
                        tile.LiquidType = LiquidID.Water;
                    }

                    if (!tile.HasTile)
                        continue;

                    int newType = WorldGen.genRand.NextFloat() < 0.8f
                        ? typeDark
                        : mechTiles[WorldGen.genRand.Next(mechTiles.Length)];
                    tile.TileType = (ushort)newType;
                }
            }
        }

        /// <summary>

        /// 用多层噪声叠加构建连续的地表高度图，出生点附近压平

        /// </summary>
        private static int[] BuildSurfaceHeightMap(int worldWidth, int worldHeight, int surfaceY, int spawnX) {
            int[] heightMap = new int[worldWidth];
            int seed = Main.ActiveWorldFileData.Seed;

            //三层噪声分别控制大尺度山脉、中尺度丘陵、小尺度细节
            FastNoiseLite noiseLarge = new(seed);
            noiseLarge.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noiseLarge.SetFrequency(0.003f);

            FastNoiseLite noiseMedium = new(seed + 1111);
            noiseMedium.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            noiseMedium.SetFrequency(0.012f);

            FastNoiseLite noiseSmall = new(seed + 2222);
            noiseSmall.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            noiseSmall.SetFrequency(0.04f);

            //出生点安全半径，此范围内地形压平
            int spawnFlatRadius = 40;
            //压平过渡带宽度
            int spawnBlendWidth = 30;

            for (int x = 0; x < worldWidth; x++) {
                //大尺度起伏，幅度约±35格
                float large = noiseLarge.GetNoise(x, 0) * 35f;
                //中尺度丘陵，幅度约±12格
                float medium = noiseMedium.GetNoise(x, 0) * 12f;
                //小尺度细节纹理，幅度约±4格
                float small = noiseSmall.GetNoise(x, 0) * 4f;

                float rawHeight = surfaceY + large + medium + small;

                //出生点附近压平处理
                int distToSpawn = Math.Abs(x - spawnX);
                if (distToSpawn < spawnFlatRadius) {
                    //完全在安全区内，强制为平坦的出生高度
                    rawHeight = surfaceY;
                }
                else if (distToSpawn < spawnFlatRadius + spawnBlendWidth) {
                    //过渡带，从平坦高度平滑过渡到自然高度
                    float blend = (distToSpawn - spawnFlatRadius) / (float)spawnBlendWidth;
                    //用平滑阶梯函数使过渡更自然
                    blend = blend * blend * (3f - 2f * blend);
                    rawHeight = MathHelper.Lerp(surfaceY, rawHeight, blend);
                }

                heightMap[x] = Math.Clamp((int)rawHeight, SafePadding + 60, worldHeight - SafePadding - 20);
            }

            return heightMap;
        }

        /// <summary>

        /// 沿高度图铺设地表层物块，包含表层和浅层填充

        /// </summary>
        private static void LayMechanicalSurface(int worldWidth, int worldHeight,
            int[] heightMap, int typeDark, int typeStone, int[] mechTiles) {
            //表层厚度（地表线往下多少格是表层）
            int surfaceDepth = 6;
            //浅层填充深度（表层以下继续填充多少格实心物块）
            int shallowFillDepth = 20;

            for (int x = SafePadding; x < worldWidth - SafePadding; x++) {
                int groundY = heightMap[x];

                //铺设表层（机械石头为主）
                for (int dy = 0; dy < surfaceDepth; dy++) {
                    int py = groundY + dy;
                    if (!InWorldSafe(x, py))
                        continue;

                    int tileType = dy < 2 ? typeStone : typeDark;
                    //偶尔混入其他机械物块增加变化
                    if (WorldGen.genRand.NextFloat() < 0.08f) {
                        tileType = mechTiles[WorldGen.genRand.Next(mechTiles.Length)];
                    }
                    WorldGen.PlaceTile(x, py, tileType, mute: true, forced: true);
                }

                //浅层填充
                for (int dy = surfaceDepth; dy < surfaceDepth + shallowFillDepth; dy++) {
                    int py = groundY + dy;
                    if (!InWorldSafe(x, py))
                        continue;

                    //只填充空气格，不覆盖原版世界已有的物块
                    Tile tile = Main.tile[x, py];
                    if (!tile.HasTile) {
                        int tileType = WorldGen.genRand.NextFloat() < 0.85f
                            ? typeDark
                            : mechTiles[WorldGen.genRand.Next(mechTiles.Length)];
                        WorldGen.PlaceTile(x, py, tileType, mute: true, forced: true);
                    }
                }

                //清理地表线以上的残留物块和墙壁，确保天空干净
                for (int py = SafePadding; py < groundY; py++) {
                    if (!InWorldSafe(x, py))
                        continue;
                    Tile tile = Main.tile[x, py];
                    if (tile.HasTile) {
                        WorldGen.KillTile(x, py, noItem: true);
                    }
                    if (tile.WallType != WallID.None) {
                        tile.WallType = WallID.None;
                    }
                }
            }
        }

        /// <summary>

        /// 在地表线上稀疏生成机械尖塔，远离出生点，形状自然

        /// </summary>
        private static void GenerateSpires(int worldWidth, int worldHeight,
            int[] heightMap, int typeStone, int spawnX) {
            //尖塔候选区间排除出生点附近50格
            int spawnExcludeRadius = 50;
            //尖塔之间最小间距，保证相邻尖塔不会重叠但密度足够高
            int minSpireSpacing = 18;
            int lastSpireX = -minSpireSpacing * 2;

            FastNoiseLite spireNoise = new(Main.ActiveWorldFileData.Seed + 7777);
            spireNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            spireNoise.SetFrequency(0.015f);

            for (int x = SafePadding + 20; x < worldWidth - SafePadding - 20; x++) {
                //排除出生点附近
                if (Math.Abs(x - spawnX) < spawnExcludeRadius)
                    continue;

                //检查与上一个尖塔的距离
                if (x - lastSpireX < minSpireSpacing)
                    continue;

                //噪声值用于调制高度，低阈值保证大部分区域都能出塔
                float spireChance = spireNoise.GetNoise(x, 0);
                if (spireChance < -0.2f)
                    continue;

                //80%通过率，保证密集但仍有间隙
                if (WorldGen.genRand.NextFloat() > 0.8f)
                    continue;

                int baseY = heightMap[x];
                //噪声值映射到高度因子，范围从-0.2到1.0映射为0到1
                float heightFactor = Math.Clamp((spireChance + 0.2f) / 1.2f, 0f, 1f);
                int spireHeight = (int)MathHelper.Lerp(15, 55, heightFactor);
                //底部宽度和高度成比例，较小的塔更细
                int baseWidth = Math.Max(4, spireHeight / 4 + WorldGen.genRand.Next(-1, 2));

                BuildSingleSpire(x, baseY, spireHeight, baseWidth, typeStone, worldHeight);

                //下一个尖塔的最小间距在基础间距上加上当前底部宽度，避免重叠
                lastSpireX = x + baseWidth;
            }
        }

        /// <summary>构建单个尖塔，使用椭圆轮廓加噪声扰动</summary>
        private static void BuildSingleSpire(int centerX, int baseY, int height, int baseWidth,
            int typeStone, int worldHeight) {
            FastNoiseLite edgeNoise = new(centerX * 31 + baseY * 17);
            edgeNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            edgeNoise.SetFrequency(0.15f);

            float halfBase = baseWidth * 0.5f;

            for (int sy = 0; sy < height; sy++) {
                //从底部到顶部的进度（0=底部，1=顶部）
                float t = sy / (float)height;
                //宽度曲线：底部较宽，中段缓慢收窄，顶部急剧收尖
                //使用抛物线的幂次方让收窄更集中在顶部
                float widthRatio = 1f - MathF.Pow(t, 1.6f);
                float currentHalfWidth = halfBase * widthRatio;

                //噪声扰动边缘让形状不那么规则
                float noiseOffset = edgeNoise.GetNoise(sy * 3f, 0) * 1.5f;
                currentHalfWidth += noiseOffset;

                if (currentHalfWidth < 0.5f)
                    currentHalfWidth = 0.5f;

                int intHalfWidth = (int)MathF.Ceiling(currentHalfWidth);
                for (int dx = -intHalfWidth; dx <= intHalfWidth; dx++) {
                    //柔和的边缘判定
                    float dist = MathF.Abs(dx);
                    if (dist > currentHalfWidth)
                        continue;

                    int px = centerX + dx;
                    int py = baseY - sy;
                    if (InWorldSafe(px, py)) {
                        WorldGen.PlaceTile(px, py, typeStone, mute: true, forced: true);
                    }
                }
            }

            //在尖塔底部两侧添加支撑结构，让塔基更稳固自然
            int buttressWidth = baseWidth / 2 + 2;
            int buttressHeight = Math.Max(4, height / 6);
            for (int side = -1; side <= 1; side += 2) {
                for (int by = 0; by < buttressHeight; by++) {
                    float bt = by / (float)buttressHeight;
                    int bw = (int)(buttressWidth * (1f - bt));
                    for (int dx = 0; dx < bw; dx++) {
                        int px = centerX + side * ((int)halfBase + dx);
                        int py = baseY + by;
                        if (InWorldSafe(px, py)) {
                            WorldGen.PlaceTile(px, py, typeStone, mute: true, forced: true);
                        }
                    }
                }
            }
        }

        /// <summary>在出生点清理出平坦的着陆区域</summary>
        private static void ClearSpawnArea(int worldWidth, int worldHeight,
            int[] heightMap, int spawnX) {
            //着陆平台半宽
            int platformHalfWidth = 25;
            //着陆平台上方清空高度
            int clearHeight = 50;

            int spawnSurfaceY = heightMap[Math.Clamp(spawnX, 0, worldWidth - 1)];

            //设置出生点坐标
            Main.spawnTileX = spawnX;
            Main.spawnTileY = spawnSurfaceY - 2;

            //清理出生区域上方的所有物块
            for (int x = spawnX - platformHalfWidth; x <= spawnX + platformHalfWidth; x++) {
                if (x < SafePadding || x >= worldWidth - SafePadding)
                    continue;

                for (int y = spawnSurfaceY - clearHeight; y < spawnSurfaceY; y++) {
                    if (InWorldSafe(x, y)) {
                        WorldGen.KillTile(x, y, noItem: true);
                    }
                }
            }

            //在出生点地面铺一层平坦的机械物块作为着陆平台
            int platformTile = ModContent.TileType<MechanicalStone>();
            for (int x = spawnX - platformHalfWidth; x <= spawnX + platformHalfWidth; x++) {
                if (x < SafePadding || x >= worldWidth - SafePadding)
                    continue;

                //铺3格厚的平台
                for (int dy = 0; dy < 3; dy++) {
                    int py = spawnSurfaceY + dy;
                    if (InWorldSafe(x, py)) {
                        WorldGen.PlaceTile(x, py, platformTile, mute: true, forced: true);
                    }
                }
            }
        }

        /// <summary>

        /// 沿实际地表面放置机械装饰物（管道、金属棒等）

        /// </summary>
        private static void PlaceSurfaceDecorations(int worldWidth, int worldHeight, int[] heightMap) {
            int[] decoTypes = [TileID.MetalBars, TileID.IronBrick, TileID.Cog];

            for (int x = SafePadding; x < worldWidth - SafePadding; x += WorldGen.genRand.Next(12, 30)) {
                //只有15%的概率放置装饰，降低密度
                if (WorldGen.genRand.NextFloat() >= 0.15f)
                    continue;

                int groundY = heightMap[Math.Clamp(x, 0, worldWidth - 1)];
                int decoY = groundY - 1;

                if (!InWorldSafe(x, groundY) || !InWorldSafe(x, decoY))
                    continue;

                //确保地面有物块且上方为空
                if (!Framing.GetTileSafely(x, groundY).HasTile)
                    continue;
                if (Framing.GetTileSafely(x, decoY).HasTile)
                    continue;

                int decoType = decoTypes[WorldGen.genRand.Next(decoTypes.Length)];
                WorldGen.PlaceTile(x, decoY, decoType, mute: true);
            }
        }
    }
}
