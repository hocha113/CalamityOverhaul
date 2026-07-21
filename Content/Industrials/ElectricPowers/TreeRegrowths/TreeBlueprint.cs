using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths
{
    /// <summary>
    /// 树结构蓝图，按原版 <see cref="WorldGen.GrowTree"/> 帧表以种子预生成
    /// <br>动画与落地写块共用同一份数据</br>
    /// </summary>
    internal class TreeBlueprint
    {
        /// <summary>单块，帧同原版树tile</summary>
        internal struct Piece
        {
            /// <summary>相对主干列的横向偏移(-1/0/+1)</summary>
            public sbyte OffsetX;
            /// <summary>物块世界Y</summary>
            public int TileY;
            public short FrameX;
            public short FrameY;
            public byte FrameNumber;

            /// <summary>主干顶帽(frameX 22 带树冠 / 0 秃顶)</summary>
            public readonly bool IsTopStub => OffsetX == 0 && FrameY >= 198;
            /// <summary>带叶侧枝(frameX 44 向左伸 / 66 向右伸)</summary>
            public readonly bool IsLeafyBranch => OffsetX != 0 && FrameY >= 198;
        }

        /// <summary>树 tile 类型(5/323/596/616/634)</summary>
        public int TreeTileType;
        /// <summary>主干列X</summary>
        public int TrunkX;
        /// <summary>地面物块Y，树体从 GroundY-1 往上</summary>
        public int GroundY;
        /// <summary>主干高度(格)</summary>
        public int Height;
        public List<Piece> Pieces = [];

        public bool IsPalm => TreeTileType == TileID.PalmTree;

        #region 树种解析
        /// <summary>地面→树种；preferredType仅草地保樱花/柳树，失败0</summary>
        public static int ResolveTreeType(int x, int groundY, int preferredType) {
            if (!WorldGen.InWorld(x, groundY, 30)) {
                return 0;
            }
            Tile ground = Main.tile[x, groundY];
            if (!ground.HasTile) {
                return 0;
            }
            int type = ground.TileType;
            if (IsPalmGround(type)) {
                return TileID.PalmTree;
            }
            if (type == TileID.Ash) {
                return TileID.TreeAsh;
            }
            if ((preferredType == TileID.VanityTreeSakura || preferredType == TileID.VanityTreeYellowWillow)
                && TileID.Sets.Conversion.Grass[type]) {
                return preferredType;
            }
            if (IsCommonTreeGround(type)) {
                return TileID.Trees;
            }
            return 0;
        }

        private static bool IsPalmGround(int type) {
            return type == TileID.Sand || type == TileID.Crimsand
                || type == TileID.Pearlsand || type == TileID.Ebonsand;
        }

        //原版 IsTileTypeFitForTree 的纯原版子集(633灰烬归灰烬树，不走普通树)
        private static bool IsCommonTreeGround(int type) {
            return type == TileID.Grass || type == TileID.GolfGrass
                || type == TileID.CorruptGrass || type == TileID.CorruptJungleGrass
                || type == TileID.JungleGrass || type == TileID.MushroomGrass
                || type == TileID.HallowedGrass || type == TileID.GolfGrassHallowed
                || type == TileID.SnowBlock
                || type == TileID.CrimsonGrass || type == TileID.CrimsonJungleGrass;
        }
        #endregion

        #region 生成
        /// <summary>按种子生成；地面与树种不匹配返回false</summary>
        public static bool TryGenerate(int x, int groundY, int treeTileType, int seed, out TreeBlueprint blueprint) {
            blueprint = null;
            if (!WorldGen.InWorld(x, groundY, 30)) {
                return false;
            }
            Tile ground = Main.tile[x, groundY];
            if (!ground.HasTile) {
                return false;
            }

            int groundType = ground.TileType;
            UnifiedRandom rand = new UnifiedRandom(seed);

            switch (treeTileType) {
                case TileID.PalmTree:
                    if (!IsPalmGround(groundType)) {
                        return false;
                    }
                    blueprint = GeneratePalm(x, groundY, rand);
                    break;
                case TileID.Trees:
                    if (!IsCommonTreeGround(groundType)) {
                        return false;
                    }
                    blueprint = GenerateCommon(x, groundY, rand);
                    break;
                case TileID.VanityTreeSakura:
                case TileID.VanityTreeYellowWillow:
                    if (!TileID.Sets.Conversion.Grass[groundType]) {
                        return false;
                    }
                    blueprint = GenerateWithSettings(x, groundY, treeTileType, rand);
                    break;
                case TileID.TreeAsh:
                    if (groundType != TileID.Ash) {
                        return false;
                    }
                    blueprint = GenerateWithSettings(x, groundY, treeTileType, rand);
                    break;
                default:
                    return false;
            }
            return blueprint != null;
        }

        //num5 → 主干帧(镜像 GrowTree 的 switch)
        private static (short fx, short fy) TrunkFrame(int shape, int row) {
            short fy22 = (short)(row * 22);
            short fy66 = (short)(66 + row * 22);
            return shape switch {
                1 => (0, fy66),
                2 => (22, fy22),
                3 => (44, fy66),
                4 => (22, fy66),
                5 => (88, fy22),   //带左枝桩
                6 => (66, fy66),   //带右枝桩
                7 => (110, fy66),  //双枝桩
                _ => (0, fy22)
            };
        }

        //根部旁贴地检测(镜像 GrowTree 的 nactive+halfBrick+slope+IsTileTypeFitForTree)
        private static bool RootGroundFit(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasUnactuatedTile && !tile.IsHalfBlock && tile.Slope == SlopeType.Solid
                && WorldGen.IsTileTypeFitForTree(tile.TileType);
        }

        /// <summary>普通树(tile5)，镜像 GrowTree 帧表</summary>
        private static TreeBlueprint GenerateCommon(int i, int j, UnifiedRandom rand) {
            TreeBlueprint bp = new TreeBlueprint {
                TreeTileType = TileID.Trees,
                TrunkX = i,
                GroundY = j,
                Height = rand.Next(5, 17)
            };
            int height = bp.Height;
            //remix世界地表以上不长树冠树枝(原版 flag2)
            bool noFoliage = Main.remixWorld && j < Main.worldSurface;

            bool leftTaken = false, rightTaken = false;
            int bottomIdx = -1, topIdx = -1;

            for (int k = j - height; k < j; k++) {
                byte frameNumber = (byte)rand.Next(3);
                int row = rand.Next(3);
                int shape = rand.Next(10);
                if (k == j - 1 || k == j - height) {
                    shape = 0;
                }
                while (((shape == 5 || shape == 7) && leftTaken) || ((shape == 6 || shape == 7) && rightTaken)) {
                    shape = rand.Next(10);
                }
                leftTaken = shape == 5 || shape == 7;
                rightTaken = shape == 6 || shape == 7;

                (short fx, short fy) = TrunkFrame(shape, row);
                int idx = bp.Pieces.Count;
                bp.Pieces.Add(new Piece { OffsetX = 0, TileY = k, FrameX = fx, FrameY = fy, FrameNumber = frameNumber });
                if (k == j - 1) {
                    bottomIdx = idx;
                }
                if (k == j - height) {
                    topIdx = idx;
                }

                if (shape == 5 || shape == 7) {
                    int bRow = rand.Next(3);
                    bool leafy = rand.Next(3) < 2 && !noFoliage;
                    bp.Pieces.Add(new Piece {
                        OffsetX = -1, TileY = k,
                        FrameX = leafy ? (short)44 : (short)66,
                        FrameY = (short)(leafy ? 198 + bRow * 22 : bRow * 22)
                    });
                }
                if (shape == 6 || shape == 7) {
                    int bRow = rand.Next(3);
                    bool leafy = rand.Next(3) < 2 && !noFoliage;
                    bp.Pieces.Add(new Piece {
                        OffsetX = 1, TileY = k,
                        FrameX = leafy ? (short)66 : (short)88,
                        FrameY = (short)(leafy ? 198 + bRow * 22 : 66 + bRow * 22)
                    });
                }
            }

            //根部num6 0双1右2左3无(照抄原版)
            int rootCase = rand.Next(3);
            bool leftFit = RootGroundFit(i - 1, j);
            bool rightFit = RootGroundFit(i + 1, j);
            if (!leftFit) {
                if (rootCase == 0) {
                    rootCase = 2;
                }
                if (rootCase == 1) {
                    rootCase = 3;
                }
            }
            if (!rightFit) {
                if (rootCase == 0) {
                    rootCase = 1;
                }
                if (rootCase == 2) {
                    rootCase = 3;
                }
            }
            if (leftFit && !rightFit) {
                rootCase = 2;
            }
            if (rightFit && !leftFit) {
                rootCase = 1;
            }

            if (rootCase == 0 || rootCase == 1) {
                int row = rand.Next(3);
                bp.Pieces.Add(new Piece { OffsetX = 1, TileY = j - 1, FrameX = 22, FrameY = (short)(132 + row * 22) });
            }
            if (rootCase == 0 || rootCase == 2) {
                int row = rand.Next(3);
                bp.Pieces.Add(new Piece { OffsetX = -1, TileY = j - 1, FrameX = 44, FrameY = (short)(132 + row * 22) });
            }
            int centerRow = rand.Next(3);
            if (rootCase != 3 && bottomIdx >= 0) {
                Piece bottom = bp.Pieces[bottomIdx];
                bottom.FrameX = rootCase switch { 0 => (short)88, 1 => (short)0, _ => (short)66 };
                bottom.FrameY = (short)(132 + centerRow * 22);
                bp.Pieces[bottomIdx] = bottom;
            }

            //顶帽1/13秃顶(frameX0)
            if (topIdx >= 0) {
                bool leafyTop = rand.Next(13) != 0 && !noFoliage;
                int topRow = rand.Next(3);
                Piece top = bp.Pieces[topIdx];
                top.FrameX = leafyTop ? (short)22 : (short)0;
                top.FrameY = (short)(198 + topRow * 22);
                bp.Pieces[topIdx] = top;
            }
            return bp;
        }

        /// <summary>樱花/柳/灰烬，镜像 GrowTreeWithSettings(高7-12)</summary>
        private static TreeBlueprint GenerateWithSettings(int i, int j, int treeTileType, UnifiedRandom rand) {
            TreeBlueprint bp = new TreeBlueprint {
                TreeTileType = treeTileType,
                TrunkX = i,
                GroundY = j,
                Height = rand.Next(7, 13)
            };
            int height = bp.Height;

            bool leftTaken = false, rightTaken = false;
            int bottomIdx = -1, topIdx = -1;

            for (int k = j - height; k < j; k++) {
                byte frameNumber = (byte)rand.Next(3);
                int row = rand.Next(3);
                int shape = rand.Next(10);
                if (k == j - 1 || k == j - height) {
                    shape = 0;
                }
                while (((shape == 5 || shape == 7) && leftTaken) || ((shape == 6 || shape == 7) && rightTaken)) {
                    shape = rand.Next(10);
                }
                leftTaken = shape == 5 || shape == 7;
                rightTaken = shape == 6 || shape == 7;

                (short fx, short fy) = TrunkFrame(shape, row);
                int idx = bp.Pieces.Count;
                bp.Pieces.Add(new Piece { OffsetX = 0, TileY = k, FrameX = fx, FrameY = fy, FrameNumber = frameNumber });
                if (k == j - 1) {
                    bottomIdx = idx;
                }
                if (k == j - height) {
                    topIdx = idx;
                }

                if (shape == 5 || shape == 7) {
                    int bRow = rand.Next(3);
                    bool leafy = rand.Next(3) < 2;
                    bp.Pieces.Add(new Piece {
                        OffsetX = -1, TileY = k,
                        FrameX = leafy ? (short)44 : (short)66,
                        FrameY = (short)(leafy ? 198 + bRow * 22 : bRow * 22)
                    });
                }
                if (shape == 6 || shape == 7) {
                    int bRow = rand.Next(3);
                    bool leafy = rand.Next(3) < 2;
                    bp.Pieces.Add(new Piece {
                        OffsetX = 1, TileY = k,
                        FrameX = leafy ? (short)66 : (short)88,
                        FrameY = (short)(leafy ? 198 + bRow * 22 : 66 + bRow * 22)
                    });
                }
            }

            bool leftRoot = RootGroundFit(i - 1, j);
            bool rightRoot = RootGroundFit(i + 1, j);
            if (rand.Next(3) == 0) {
                leftRoot = false;
            }
            if (rand.Next(3) == 0) {
                rightRoot = false;
            }
            if (rightRoot) {
                int row = rand.Next(3);
                bp.Pieces.Add(new Piece { OffsetX = 1, TileY = j - 1, FrameX = 22, FrameY = (short)(132 + row * 22) });
            }
            if (leftRoot) {
                int row = rand.Next(3);
                bp.Pieces.Add(new Piece { OffsetX = -1, TileY = j - 1, FrameX = 44, FrameY = (short)(132 + row * 22) });
            }
            int centerRow = rand.Next(3);
            if ((leftRoot || rightRoot) && bottomIdx >= 0) {
                Piece bottom = bp.Pieces[bottomIdx];
                bottom.FrameX = leftRoot && rightRoot ? (short)88 : leftRoot ? (short)0 : (short)66;
                bottom.FrameY = (short)(132 + centerRow * 22);
                bp.Pieces[bottomIdx] = bottom;
            }

            if (topIdx >= 0) {
                bool leafyTop = rand.Next(13) != 0;
                int topRow = rand.Next(3);
                Piece top = bp.Pieces[topIdx];
                top.FrameX = leafyTop ? (short)22 : (short)0;
                top.FrameY = (short)(198 + topRow * 22);
                bp.Pieces[topIdx] = top;
            }
            return bp;
        }

        /// <summary>棕榈，镜像 GrowPalmTree；frameY=横倾像素</summary>
        private static TreeBlueprint GeneratePalm(int i, int groundY, UnifiedRandom rand) {
            TreeBlueprint bp = new TreeBlueprint {
                TreeTileType = TileID.PalmTree,
                TrunkX = i,
                GroundY = groundY,
                Height = rand.Next(10, 21)
            };
            int height = bp.Height;
            int leanTarget = rand.Next(-8, 9) * 2;
            short lean = 0;

            for (int j = 0; j < height; j++) {
                int y = groundY - 1 - j;
                if (j == 0) {
                    bp.Pieces.Add(new Piece { OffsetX = 0, TileY = y, FrameX = 66, FrameY = 0 });
                    continue;
                }
                if (j == height - 1) {
                    //顶帽(frameX 88/110/132)不画树块本体，画棕榈树冠
                    bp.Pieces.Add(new Piece { OffsetX = 0, TileY = y, FrameX = (short)(22 * rand.Next(4, 7)), FrameY = lean });
                    continue;
                }
                if (lean != leanTarget) {
                    double p = j / (double)height;
                    if (!(p < 0.25)) {
                        //原版此处按高度段消耗随机数后固定步进2px
                        if ((!(p < 0.5) || rand.Next(13) != 0) && (!(p < 0.7) || rand.Next(9) != 0) && p < 0.95) {
                            rand.Next(5);
                        }
                        lean += (short)(Math.Sign(leanTarget) * 2);
                    }
                }
                bp.Pieces.Add(new Piece { OffsetX = 0, TileY = y, FrameX = (short)(22 * rand.Next(0, 3)), FrameY = lean });
            }
            return bp;
        }
        #endregion

        #region 落地校验与写入
        /// <summary>权威端滚可用种子，蓝图整体校验过才算(高度可能撞上限)</summary>
        public static bool TryRollSeed(int x, int groundY, int treeTileType, out int seed) {
            //随机高度可能撞上头顶空间上限，多滚几次提高出苗率
            for (int attempt = 0; attempt < 8; attempt++) {
                seed = Main.rand.Next(int.MaxValue);
                if (TryGenerate(x, groundY, treeTileType, seed, out TreeBlueprint blueprint) && blueprint.CanPlace()) {
                    return true;
                }
            }
            seed = 0;
            return false;
        }

        /// <summary>写块前复检(镜像原版前置；落地前世界可能变)</summary>
        public bool CanPlace() {
            if (!WorldGen.InWorld(TrunkX, GroundY, 32)) {
                return false;
            }
            return TreeTileType switch {
                TileID.PalmTree => CanPlacePalm(),
                TileID.Trees => CanPlaceCommon(),
                _ => CanPlaceWithSettings()
            };
        }

        private bool CanPlaceCommon() {
            int i = TrunkX, j = GroundY;
            if (Main.tile[i - 1, j - 1].LiquidAmount != 0 || Main.tile[i, j - 1].LiquidAmount != 0 || Main.tile[i + 1, j - 1].LiquidAmount != 0) {
                return false;
            }
            Tile ground = Main.tile[i, j];
            if (!ground.HasUnactuatedTile || ground.IsHalfBlock || ground.Slope != SlopeType.Solid || !IsCommonTreeGround(ground.TileType)) {
                return false;
            }
            int wall = Main.tile[i, j - 1].WallType;
            bool wallOK = (Main.remixWorld && j > Main.worldSurface) || wall == 0 || WorldGen.DefaultTreeWallTest(wall);
            if (!wallOK) {
                return false;
            }
            bool leftFit = Main.tile[i - 1, j].HasTile && WorldGen.IsTileTypeFitForTree(Main.tile[i - 1, j].TileType);
            bool rightFit = Main.tile[i + 1, j].HasTile && WorldGen.IsTileTypeFitForTree(Main.tile[i + 1, j].TileType);
            if (!leftFit && !rightFit) {
                return false;
            }
            int padded = Height + 4;
            if (ground.TileType == TileID.JungleGrass) {
                padded += 5;
            }
            return WorldGen.EmptyTileCheck(i - 2, i + 2, j - padded, j - 1, TileID.Saplings);
        }

        private bool CanPlaceWithSettings() {
            int i = TrunkX, j = GroundY;
            if (Main.tile[i - 1, j - 1].LiquidAmount != 0 || Main.tile[i, j - 1].LiquidAmount != 0 || Main.tile[i + 1, j - 1].LiquidAmount != 0) {
                return false;
            }
            Tile ground = Main.tile[i, j];
            if (!ground.HasUnactuatedTile || ground.IsHalfBlock || ground.Slope != SlopeType.Solid) {
                return false;
            }
            bool groundOK = TreeTileType == TileID.TreeAsh
                ? ground.TileType == TileID.Ash
                : TileID.Sets.Conversion.Grass[ground.TileType];
            if (!groundOK) {
                return false;
            }
            if (!WorldGen.DefaultTreeWallTest(Main.tile[i, j - 1].WallType)) {
                return false;
            }
            bool leftFit = Main.tile[i - 1, j].HasTile && GroundTestSettings(Main.tile[i - 1, j].TileType);
            bool rightFit = Main.tile[i + 1, j].HasTile && GroundTestSettings(Main.tile[i + 1, j].TileType);
            if (!leftFit && !rightFit) {
                return false;
            }
            return WorldGen.EmptyTileCheck(i - 2, i + 2, j - (Height + 4), j - 1, TileID.Saplings);
        }

        private bool GroundTestSettings(int type) {
            return TreeTileType == TileID.TreeAsh ? type == TileID.Ash : TileID.Sets.Conversion.Grass[type];
        }

        private bool CanPlacePalm() {
            int i = TrunkX, j = GroundY;
            Tile ground = Main.tile[i, j];
            if (!ground.HasTile || ground.IsHalfBlock || ground.Slope != SlopeType.Solid || !IsPalmGround(ground.TileType)) {
                return false;
            }
            Tile above = Main.tile[i, j - 1];
            if (above.WallType != WallID.None || above.LiquidAmount != 0) {
                return false;
            }
            return WorldGen.EmptyTileCheck(i, i, j - 2, j - 1, TileID.Saplings)
                && WorldGen.EmptyTileCheck(i - 1, i + 1, j - 30, j - 3, TileID.Saplings);
        }

        /// <summary>
        /// 写蓝图入世界并同步(权威端)；帧即动画所见
        /// <br>覆写草花，同原版 GrowTree(EmptyTileCheck无视)</br>
        /// </summary>
        public void Place() {
            Tile ground = Main.tile[TrunkX, GroundY];
            byte paint = ground.TileColor;
            bool fullbright = ground.IsTileFullbright;
            bool invisible = ground.IsTileInvisible;

            foreach (Piece piece in Pieces) {
                Tile tile = Main.tile[TrunkX + piece.OffsetX, piece.TileY];
                tile.HasTile = true;
                tile.TileType = (ushort)TreeTileType;
                tile.TileFrameX = piece.FrameX;
                tile.TileFrameY = piece.FrameY;
                tile.TileFrameNumber = piece.FrameNumber;
                tile.TileColor = paint;
                tile.IsTileFullbright = fullbright;
                tile.IsTileInvisible = invisible;
                tile.IsHalfBlock = false;
                tile.Slope = SlopeType.Solid;
                tile.IsActuated = false;
            }

            WorldGen.RangeFrame(TrunkX - 2, GroundY - Height - 1, TrunkX + 2, GroundY + 1);
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, TrunkX - 1, GroundY - Height, 3, Height);
            }
            //原版长树的撒叶演出(服务器广播112，单机直接播)
            WorldGen.TreeGrowFXCheck(TrunkX, GroundY - 1);
        }
        #endregion
    }
}
