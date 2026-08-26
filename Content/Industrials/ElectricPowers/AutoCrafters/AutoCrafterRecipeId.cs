using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>
    /// 配方身份与解析:钉选存"产物 type + 产物数量 + 原料无序哈希",不存配方下标。<br/>
    /// 模组增删会重排 <see cref="Main.recipe"/>,裸下标在更新后会静默合成错误的东西;
    /// 载入时按身份重解析,找不到就把钉选置空并在 UI 亮"配方已失效"
    /// </summary>
    internal static class AutoCrafterRecipeId
    {
        /// <summary>原料无序哈希:按 type 排序后对 (type,stack) 序列做 FNV-1a</summary>
        public static int ComputeIngredientHash(Recipe recipe) {
            List<(int Type, int Stack)> pairs = new(recipe.requiredItem.Count);
            foreach (Item req in recipe.requiredItem) {
                if (req == null || req.IsAir) {
                    continue;
                }
                pairs.Add((req.type, req.stack));
            }
            pairs.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : a.Stack.CompareTo(b.Stack));

            unchecked {
                uint hash = 2166136261u;
                foreach ((int type, int stack) in pairs) {
                    hash = (hash ^ (uint)type) * 16777619u;
                    hash = (hash ^ (uint)stack) * 16777619u;
                }
                return (int)hash;
            }
        }

        /// <summary>配方可否被钉选:禁用/空产物/空原料的一律不收</summary>
        public static bool IsPinnable(Recipe recipe) {
            if (recipe == null || recipe.Disabled) {
                return false;
            }
            if (recipe.createItem == null || recipe.createItem.IsAir) {
                return false;
            }
            return recipe.requiredItem.Count > 0;
        }

        /// <summary>按身份重解析配方;同身份多站台变体取第一个命中</summary>
        public static Recipe Resolve(int resultType, int resultStack, int ingredientHash) {
            if (resultType <= ItemID.None) {
                return null;
            }
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (!IsPinnable(recipe)) {
                    continue;
                }
                if (recipe.createItem.type != resultType || recipe.createItem.stack != resultStack) {
                    continue;
                }
                if (ComputeIngredientHash(recipe) == ingredientHash) {
                    return recipe;
                }
            }
            return null;
        }

        /// <summary>列出产物为指定 type 的全部可钉选配方(UI 消费)</summary>
        public static List<Recipe> FindByResult(int resultType) {
            List<Recipe> found = [];
            if (resultType <= ItemID.None) {
                return found;
            }
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (IsPinnable(recipe) && recipe.createItem.type == resultType) {
                    found.Add(recipe);
                }
            }
            return found;
        }

        /// <summary>库存物品可否抵充某原料:同 type 或落在配方声明的任一配方组里</summary>
        public static bool MatchesIngredient(Recipe recipe, Item stored, Item required)
            => stored.type == required.type || recipe.AcceptedByItemGroups(stored.type, required.type);
    }

    /// <summary>
    /// 站台邻接快照:机器周边矩形内的制作站与液体,替代玩家的 adjTile/adjWater。<br/>
    /// 只读 tile 数据,并行阶段可扫;原版等价提升(地狱熔炉算熔炉等)照抄
    /// Player.AdjTiles 的 switch,模组瓦片走 ModTile.AdjTiles 声明
    /// </summary>
    internal class StationSnapshot
    {
        private readonly bool[] adjTile = new bool[TileLoader.TileCount];
        public bool AdjWater { get; private set; }
        public bool AdjLava { get; private set; }
        public bool AdjHoney { get; private set; }
        public bool AdjShimmer { get; private set; }

        /// <summary>扫描半径(格),与 320 像素物流半径一致</summary>
        public const int ScanRadiusTiles = 20;

        public bool HasTile(int tileType)
            => tileType >= 0 && tileType < adjTile.Length && adjTile[tileType];

        /// <summary>配方要求的全部站台是否在场</summary>
        public bool SatisfiesTiles(Recipe recipe) {
            foreach (int tileType in recipe.requiredTile) {
                if (tileType < 0) {
                    continue;
                }
                if (!HasTile(tileType)) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 已知的液体邻接条件用快照答案,未知条件交回 IsMet
        /// (机器语境下 IsMet 里引用玩家的谓词在服务器上不可靠,异常按不满足处理)
        /// </summary>
        public bool SatisfiesConditions(Recipe recipe) {
            foreach (var condition in recipe.Conditions) {
                if (condition == Terraria.Condition.NearWater) {
                    if (!AdjWater) return false;
                    continue;
                }
                if (condition == Terraria.Condition.NearLava) {
                    if (!AdjLava) return false;
                    continue;
                }
                if (condition == Terraria.Condition.NearHoney) {
                    if (!AdjHoney) return false;
                    continue;
                }
                if (condition == Terraria.Condition.NearShimmer) {
                    if (!AdjShimmer) return false;
                    continue;
                }
                bool met;
                try {
                    met = condition.IsMet();
                } catch {
                    met = false;
                }
                if (!met) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>扫描机器周边矩形,建立站台与液体快照</summary>
        public static StationSnapshot Scan(Point16 topLeft, int machineTileWidth, int machineTileHeight) {
            StationSnapshot snap = new();
            int left = topLeft.X - ScanRadiusTiles;
            int right = topLeft.X + machineTileWidth + ScanRadiusTiles;
            int top = topLeft.Y - ScanRadiusTiles;
            int bottom = topLeft.Y + machineTileHeight + ScanRadiusTiles;

            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 10)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile) {
                        int type = tile.TileType;
                        if (type >= 0 && type < snap.adjTile.Length) {
                            snap.adjTile[type] = true;
                        }
                        //原版等价提升,照抄 Player.AdjTiles 的 switch
                        switch (type) {
                            case TileID.Hellforge:
                            case 302:
                                snap.adjTile[TileID.Furnaces] = true;
                                break;
                            case TileID.AdamantiteForge:
                                snap.adjTile[TileID.Furnaces] = true;
                                snap.adjTile[TileID.Hellforge] = true;
                                break;
                            case TileID.MythrilAnvil:
                                snap.adjTile[TileID.Anvils] = true;
                                break;
                            case 354:
                            case 469:
                            case 487:
                                snap.adjTile[TileID.Tables] = true;
                                break;
                            case 355:
                                snap.adjTile[TileID.Bottles] = true;
                                snap.adjTile[TileID.Tables] = true;
                                break;
                        }
                        //模组瓦片的等价声明
                        if (TileLoader.GetTile(type) is ModTile modTile && modTile.AdjTiles != null) {
                            foreach (int adj in modTile.AdjTiles) {
                                if (adj >= 0 && adj < snap.adjTile.Length) {
                                    snap.adjTile[adj] = true;
                                }
                            }
                        }
                    }

                    //液体邻接:量阈值与原版一致(>200),记号方块集合一并认
                    if ((tile.LiquidAmount > 200 && tile.LiquidType == LiquidID.Water)
                        || TileID.Sets.CountsAsWaterSource[tile.TileType]) {
                        snap.AdjWater = true;
                    }
                    if ((tile.LiquidAmount > 200 && tile.LiquidType == LiquidID.Lava)
                        || TileID.Sets.CountsAsLavaSource[tile.TileType]) {
                        snap.AdjLava = true;
                    }
                    if ((tile.LiquidAmount > 200 && tile.LiquidType == LiquidID.Honey)
                        || TileID.Sets.CountsAsHoneySource[tile.TileType]) {
                        snap.AdjHoney = true;
                    }
                    if (tile.LiquidAmount > 200 && tile.LiquidType == LiquidID.Shimmer) {
                        snap.AdjShimmer = true;
                    }
                }
            }
            return snap;
        }
    }
}
