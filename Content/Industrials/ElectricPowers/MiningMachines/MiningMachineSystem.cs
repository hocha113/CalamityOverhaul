using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>矿物在当前世界中的矿源状态</summary>
    public enum OreWorldState : byte
    {
        /// <summary>本世界存在该矿</summary>
        Present,
        /// <summary>本世界不存在(异矿世界的另一半)</summary>
        Absent,
        /// <summary>尚未探明(困难模式矿在祭坛破坏前)</summary>
        Undetermined,
    }

    /// <summary>一条矿物规则对某台矿机的资格判定结果</summary>
    public enum OreGate : byte
    {
        /// <summary>与此地无关,勘探报告不列出</summary>
        Hidden,
        /// <summary>可产出</summary>
        Open,
        /// <summary>镐力不足</summary>
        NeedPick,
        /// <summary>缺少专属钻探模块</summary>
        NeedDrill,
        /// <summary>世界中尚未探明矿源</summary>
        NotInWorld,
    }

    /// <summary>
    /// 勘探快照:矿机下方一片柱状区域的真实方块统计。<br/>
    /// 各端从同一份 tile 数据扫出一致结果,产出判定与 UI 报告共用同一份快照
    /// </summary>
    public class MiningSurvey
    {
        /// <summary>扫描锚点(机器底沿中心,tile 坐标)</summary>
        public Point Anchor;
        public int Width;
        public int Depth;
        /// <summary>实际落在世界内的格数</summary>
        public int TotalCells;
        /// <summary>有实心方块的格数</summary>
        public int SolidTiles;

        //群系记号方块计数
        public int JungleTiles;
        public int SnowTiles;
        public int DesertTiles;
        public int CorruptTiles;
        public int CrimsonTiles;
        public int HallowTiles;

        /// <summary>矿物 ItemID → 扫描范围内的矿脉方块数</summary>
        public readonly Dictionary<int, int> OreTiles = new();
        /// <summary>范围内全部矿脉方块总数</summary>
        public int TotalOreTiles;

        //深度层按锚点 Y 判定
        public bool IsSurface => Anchor.Y < Main.worldSurface;
        public bool IsUnderground => Anchor.Y >= Main.worldSurface && Anchor.Y < Main.rockLayer;
        public bool IsUnderworld => Anchor.Y > Main.maxTilesY - 204;
        public bool IsCavern => Anchor.Y >= Main.rockLayer && !IsUnderworld;

        //群系判定阈值参照原版 SceneMetrics 的占比:丛林草这类稀疏记号阈值放低,雪沙这类地基方块阈值抬高
        public bool IsJungle => TotalCells > 0 && JungleTiles >= Math.Max(8, TotalCells / 100);
        public bool IsSnow => TotalCells > 0 && SnowTiles >= TotalCells / 14;
        public bool IsDesert => TotalCells > 0 && DesertTiles >= TotalCells / 14;
        public bool IsCorrupt => TotalCells > 0 && CorruptTiles >= TotalCells / 50;
        public bool IsCrimson => TotalCells > 0 && CrimsonTiles >= TotalCells / 50;
        public bool IsHallow => TotalCells > 0 && HallowTiles >= TotalCells / 60;

        /// <summary>矿脉富集度 0~1,提升产出判定频率</summary>
        public float VeinRichness => MathHelper.Clamp(TotalOreTiles / 300f, 0f, 1f);

        public int GetOreTiles(int itemID) => OreTiles.TryGetValue(itemID, out int count) ? count : 0;

        #region 方块归类表
        //矿脉方块 → 产出物品。留有 RegisterOreTile 扩展点,后续可挂灾厄矿等
        internal static readonly Dictionary<int, int> OreTileToItem = new();

        private static readonly HashSet<int> jungleMarks = [];
        private static readonly HashSet<int> snowMarks = [];
        private static readonly HashSet<int> desertMarks = [];
        private static readonly HashSet<int> corruptMarks = [];
        private static readonly HashSet<int> crimsonMarks = [];
        private static readonly HashSet<int> hallowMarks = [];

        internal static void LoadTileTables() {
            OreTileToItem.Clear();
            //基础八矿
            OreTileToItem[TileID.Copper] = ItemID.CopperOre;
            OreTileToItem[TileID.Tin] = ItemID.TinOre;
            OreTileToItem[TileID.Iron] = ItemID.IronOre;
            OreTileToItem[TileID.Lead] = ItemID.LeadOre;
            OreTileToItem[TileID.Silver] = ItemID.SilverOre;
            OreTileToItem[TileID.Tungsten] = ItemID.TungstenOre;
            OreTileToItem[TileID.Gold] = ItemID.GoldOre;
            OreTileToItem[TileID.Platinum] = ItemID.PlatinumOre;
            //邪恶矿与特殊矿
            OreTileToItem[TileID.Demonite] = ItemID.DemoniteOre;
            OreTileToItem[TileID.Crimtane] = ItemID.CrimtaneOre;
            OreTileToItem[TileID.Meteorite] = ItemID.Meteorite;
            OreTileToItem[TileID.Obsidian] = ItemID.Obsidian;
            OreTileToItem[TileID.Hellstone] = ItemID.Hellstone;
            OreTileToItem[TileID.FossilOre] = ItemID.FossilOre;
            //困难模式矿
            OreTileToItem[TileID.Cobalt] = ItemID.CobaltOre;
            OreTileToItem[TileID.Palladium] = ItemID.PalladiumOre;
            OreTileToItem[TileID.Mythril] = ItemID.MythrilOre;
            OreTileToItem[TileID.Orichalcum] = ItemID.OrichalcumOre;
            OreTileToItem[TileID.Adamantite] = ItemID.AdamantiteOre;
            OreTileToItem[TileID.Titanium] = ItemID.TitaniumOre;
            OreTileToItem[TileID.Chlorophyte] = ItemID.ChlorophyteOre;
            OreTileToItem[TileID.LunarOre] = ItemID.LunarOre;
            //宝石岩
            OreTileToItem[TileID.Amethyst] = ItemID.Amethyst;
            OreTileToItem[TileID.Topaz] = ItemID.Topaz;
            OreTileToItem[TileID.Sapphire] = ItemID.Sapphire;
            OreTileToItem[TileID.Emerald] = ItemID.Emerald;
            OreTileToItem[TileID.Ruby] = ItemID.Ruby;
            OreTileToItem[TileID.Diamond] = ItemID.Diamond;

            jungleMarks.Clear();
            jungleMarks.UnionWith([TileID.JungleGrass, TileID.Hive, TileID.LihzahrdBrick]);

            snowMarks.Clear();
            snowMarks.UnionWith([TileID.SnowBlock, TileID.IceBlock, TileID.BreakableIce,
                TileID.CorruptIce, TileID.FleshIce, TileID.HallowedIce]);

            desertMarks.Clear();
            desertMarks.UnionWith([TileID.Sand, TileID.Ebonsand, TileID.Crimsand, TileID.Pearlsand,
                TileID.HardenedSand, TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand, TileID.HallowHardenedSand,
                TileID.Sandstone, TileID.CorruptSandstone, TileID.CrimsonSandstone, TileID.HallowSandstone]);

            corruptMarks.Clear();
            corruptMarks.UnionWith([TileID.Ebonstone, TileID.CorruptGrass, TileID.Ebonsand,
                TileID.CorruptIce, TileID.CorruptSandstone, TileID.CorruptHardenedSand]);

            crimsonMarks.Clear();
            crimsonMarks.UnionWith([TileID.Crimstone, TileID.CrimsonGrass, TileID.Crimsand,
                TileID.FleshIce, TileID.CrimsonSandstone, TileID.CrimsonHardenedSand]);

            hallowMarks.Clear();
            hallowMarks.UnionWith([TileID.Pearlstone, TileID.HallowedGrass, TileID.Pearlsand,
                TileID.HallowedIce, TileID.HallowSandstone, TileID.HallowHardenedSand]);
        }

        internal static void UnloadTileTables() {
            OreTileToItem.Clear();
            jungleMarks.Clear();
            snowMarks.Clear();
            desertMarks.Clear();
            corruptMarks.Clear();
            crimsonMarks.Clear();
            hallowMarks.Clear();
        }
        #endregion

        /// <summary>
        /// 扫描一台矿机下方的柱状区域。只读 tile 数据,可在 TP 并行阶段调用
        /// </summary>
        /// <param name="topLeft">机器左上角 tile 坐标</param>
        /// <param name="machineTileWidth">机器占格宽</param>
        /// <param name="machineTileHeight">机器占格高</param>
        /// <param name="width">扫描宽(格)</param>
        /// <param name="depth">向下扫描深(格)</param>
        public static MiningSurvey Scan(Point16 topLeft, int machineTileWidth, int machineTileHeight, int width, int depth) {
            MiningSurvey survey = new() {
                Width = width,
                Depth = depth,
                Anchor = new Point(topLeft.X + machineTileWidth / 2, topLeft.Y + machineTileHeight),
            };

            int left = survey.Anchor.X - width / 2;
            int top = survey.Anchor.Y;
            for (int dx = 0; dx < width; dx++) {
                for (int dy = 0; dy < depth; dy++) {
                    int x = left + dx;
                    int y = top + dy;
                    if (!WorldGen.InWorld(x, y, 10)) {
                        continue;
                    }
                    survey.TotalCells++;
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile) {
                        continue;
                    }
                    survey.SolidTiles++;
                    int type = tile.TileType;

                    if (jungleMarks.Contains(type)) survey.JungleTiles++;
                    else if (snowMarks.Contains(type)) survey.SnowTiles++;
                    else if (desertMarks.Contains(type)) survey.DesertTiles++;
                    if (corruptMarks.Contains(type)) survey.CorruptTiles++;
                    else if (crimsonMarks.Contains(type)) survey.CrimsonTiles++;
                    else if (hallowMarks.Contains(type)) survey.HallowTiles++;

                    if (OreTileToItem.TryGetValue(type, out int itemID)) {
                        survey.OreTiles[itemID] = survey.GetOreTiles(itemID) + 1;
                        survey.TotalOreTiles++;
                    }
                }
            }
            return survey;
        }
    }

    /// <summary>矿机升级模块的效果契约,由模块物品实现</summary>
    public interface IMiningModule
    {
        /// <summary>附加镐力</summary>
        float PickPowerBonus { get; }
        /// <summary>作业周期乘数(小于 1 为加速)</summary>
        float WorkIntervalMult { get; }
        /// <summary>产出判定概率乘数</summary>
        float YieldChanceMult { get; }
        /// <summary>周期能耗乘数(小于 1 为省电)</summary>
        float EnergyCostMult { get; }
        /// <summary>稀有副产物(宝石/化石/陨石/残料)权重乘数</summary>
        float RareByproductMult { get; }
        /// <summary>矿脉加成权重乘数</summary>
        float VeinWeightMult { get; }
        /// <summary>勘探范围乘数(宽与深)</summary>
        float ScanSizeMult { get; }
        /// <summary>产出翻倍概率 0~1</summary>
        float DoubleDropChance { get; }
        /// <summary>产出按原版配比现场熔炼成锭</summary>
        bool SmeltOutput { get; }
        /// <summary>产出直接存入近旁存储</summary>
        bool ChestDeposit { get; }
        /// <summary>把本模块解锁的矿物 ItemID 写入集合(专属钻探)</summary>
        void CollectUnlockOres(HashSet<int> into);
        /// <summary>把本模块的定向权重倍率写入表(矿物 ItemID → 乘数)</summary>
        void CollectOreFocus(Dictionary<int, float> into);
    }

    /// <summary>一次产出判定/报告生成所需的矿机状态</summary>
    public struct MiningContext
    {
        public int Tier;
        /// <summary>机器等效镐力(基础+模块)</summary>
        public float PickPower;
        /// <summary>勘探快照,可为 null(视为空地)</summary>
        public MiningSurvey Survey;
        /// <summary>专属钻探模块解锁的矿物集合,可为 null</summary>
        public HashSet<int> UnlockedOres;
        /// <summary>稀有副产物权重乘数,0 视为 1(裸 struct 安全)</summary>
        public float RareBonus;
        /// <summary>矿脉加成权重乘数,0 视为 1</summary>
        public float VeinMult;
        /// <summary>定向钻头的权重倍率表(矿物 ItemID → 乘数),可为 null</summary>
        public Dictionary<int, float> OreFocus;

        public readonly bool HasUnlock(int itemID) => UnlockedOres != null && UnlockedOres.Contains(itemID);
        public readonly int VeinTiles(int itemID) => Survey?.GetOreTiles(itemID) ?? 0;
        public readonly float RareMultOrOne => RareBonus > 0f ? RareBonus : 1f;
        public readonly float VeinMultOrOne => VeinMult > 0f ? VeinMult : 1f;
        public readonly float FocusMult(int itemID)
            => OreFocus != null && OreFocus.TryGetValue(itemID, out float mult) ? mult : 1f;
    }

    /// <summary>矿物掉落规则:资格判定与权重计算一体,产出掷骰与 UI 报告共用</summary>
    public class OreDropRule
    {
        public int ItemID { get; set; }
        /// <summary>环境资格满足时的基础权重</summary>
        public float BaseWeight { get; set; }
        /// <summary>产出所需镐力(专属钻探模块豁免)</summary>
        public float RequiredPick { get; set; }
        /// <summary>需要专属钻探模块,模块同时豁免镐力门槛</summary>
        public bool NeedDedicatedDrill { get; set; }
        /// <summary>稀有副产物(宝石/化石/陨石/残料),吃勘探强化的权重乘数</summary>
        public bool IsRareByproduct { get; set; }
        public int MinTier { get; set; } = 1;
        /// <summary>每块附近矿脉方块的加成权重</summary>
        public float VeinWeightPerTile { get; set; } = 0.6f;
        /// <summary>矿脉加成计数上限</summary>
        public int VeinTileCap { get; set; } = 160;
        /// <summary>环境资格(群系/地层),null 表示处处成立;不满足但附近有真实矿脉时仍然放行</summary>
        public Func<MiningContext, bool> Relevance { get; set; }
        /// <summary>进度资格(困难模式等),不满足时直接隐藏</summary>
        public Func<MiningContext, bool> Progress { get; set; }
        /// <summary>世界矿源状态(异矿二选一/祭坛未破),null 表示恒存在</summary>
        public Func<OreWorldState> WorldState { get; set; }

        public OreDropRule(int itemID, float baseWeight, float requiredPick = 0f) {
            ItemID = itemID;
            BaseWeight = baseWeight;
            RequiredPick = requiredPick;
        }

        public OreDropRule SetRelevance(Func<MiningContext, bool> relevance) {
            Relevance = relevance;
            return this;
        }

        public OreDropRule SetProgress(Func<MiningContext, bool> progress) {
            Progress = progress;
            return this;
        }

        public OreDropRule SetWorldState(Func<OreWorldState> worldState) {
            WorldState = worldState;
            return this;
        }

        public OreDropRule SetDrillLock() {
            NeedDedicatedDrill = true;
            return this;
        }

        public OreDropRule SetRare() {
            IsRareByproduct = true;
            return this;
        }

        public OreDropRule SetVein(float perTile, int cap) {
            VeinWeightPerTile = perTile;
            VeinTileCap = cap;
            return this;
        }

        public OreDropRule SetMinTier(int tier) {
            MinTier = tier;
            return this;
        }

        /// <summary>
        /// 判定这条规则对当前矿机的资格并给出权重。
        /// 资格不满足时权重为 0,门控原因供 UI 报告展示
        /// </summary>
        public OreGate Evaluate(in MiningContext ctx, out float weight) {
            weight = 0f;
            if (ctx.Tier < MinTier) {
                return OreGate.Hidden;
            }

            int vein = ctx.VeinTiles(ItemID);
            bool relevant = Relevance?.Invoke(ctx) ?? true;
            //环境不符且附近没有真实矿脉 → 与此地无关
            if (!relevant && vein <= 0) {
                return OreGate.Hidden;
            }
            //进度未到一律隐藏,不做剧透
            if (Progress != null && !Progress(ctx)) {
                return OreGate.Hidden;
            }

            OreWorldState world = WorldState?.Invoke() ?? OreWorldState.Present;
            if (world == OreWorldState.Absent && vein <= 0) {
                return OreGate.Hidden;
            }
            if (world == OreWorldState.Undetermined && vein <= 0) {
                return OreGate.NotInWorld;
            }

            if (NeedDedicatedDrill) {
                if (!ctx.HasUnlock(ItemID)) {
                    return OreGate.NeedDrill;
                }
            }
            else if (ctx.PickPower < RequiredPick) {
                return OreGate.NeedPick;
            }

            float value = 0f;
            if (relevant) {
                value += BaseWeight;
            }
            if (vein > 0) {
                //矿脉聚焦模块放大真实矿脉的话语权
                value += Math.Min(vein, VeinTileCap) * VeinWeightPerTile * ctx.VeinMultOrOne;
            }
            //纯矿脉驱动的矿(如陨石)在附近无脉时不列出
            if (value <= 0f) {
                return OreGate.Hidden;
            }

            //勘探强化只作用于稀有副产物;定向钻头对目标矿整体放大
            if (IsRareByproduct) {
                value *= ctx.RareMultOrOne;
            }
            value *= ctx.FocusMult(ItemID);

            weight = value;
            return OreGate.Open;
        }
    }

    /// <summary>勘探报告中的一行,UI 直接消费</summary>
    public struct OreReportEntry
    {
        public int ItemID;
        public OreGate Gate;
        /// <summary>可产出条目在总权重中的占比 0~1</summary>
        public float Share;
        /// <summary>附近矿脉方块数</summary>
        public int VeinTiles;
        public float RequiredPick;
    }

    public class MiningMachineSystem : ModSystem
    {
        internal static List<OreDropRule> DropRules = new();

        /// <summary>矿→锭熔炼表(矿物 ItemID → 锭与每锭矿数),由原版配方解析,现场熔炼模块消费</summary>
        internal static readonly Dictionary<int, (int BarType, int OreCost)> SmeltTable = new();

        //参与现场熔炼的矿物白名单;比率一律从原版配方解析,
        //解析不到的(狱岩锭要黑曜石这类多材料配方)自动不参与
        private static readonly int[] smeltCandidates = [
            ItemID.CopperOre, ItemID.TinOre, ItemID.IronOre, ItemID.LeadOre,
            ItemID.SilverOre, ItemID.TungstenOre, ItemID.GoldOre, ItemID.PlatinumOre,
            ItemID.DemoniteOre, ItemID.CrimtaneOre, ItemID.Meteorite,
            ItemID.CobaltOre, ItemID.PalladiumOre, ItemID.MythrilOre, ItemID.OrichalcumOre,
            ItemID.AdamantiteOre, ItemID.TitaniumOre, ItemID.ChlorophyteOre, ItemID.LunarOre,
        ];

        public override void OnModLoad() {
            MiningSurvey.LoadTileTables();
            DropRules = [];
            LoadStandardOres();
            LoadSpecialOres();
        }

        public override void PostAddRecipes() {
            //只认原版"单一矿材 → 单件产物"的配方,熔炼比例永远与原版一致
            SmeltTable.Clear();
            foreach (int ore in smeltCandidates) {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.Mod != null || recipe.createItem.stack != 1
                        || recipe.requiredItem.Count != 1 || recipe.requiredItem[0].type != ore) {
                        continue;
                    }
                    SmeltTable[ore] = (recipe.createItem.type, recipe.requiredItem[0].stack);
                    break;
                }
            }
        }

        public override void Unload() {
            MiningSurvey.UnloadTileTables();
            SmeltTable.Clear();
            DropRules = null;
        }

        //异矿二选一:世界记录的层级瓦片与本矿匹配则存在;-1 表示祭坛尚未破坏
        private static Func<OreWorldState> AltOre(Func<int> savedTier, int tileType) {
            return () => {
                int saved = savedTier();
                if (saved < 0) {
                    return OreWorldState.Undetermined;
                }
                return saved == tileType ? OreWorldState.Present : OreWorldState.Absent;
            };
        }

        private static void LoadStandardOres() {
            //基础八矿:按世界实际生成的那一半放行,权重随价值递减
            Register(new OreDropRule(ItemID.CopperOre, 10f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Copper, TileID.Copper)));
            Register(new OreDropRule(ItemID.TinOre, 10f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Copper, TileID.Tin)));
            Register(new OreDropRule(ItemID.IronOre, 9f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Iron, TileID.Iron)));
            Register(new OreDropRule(ItemID.LeadOre, 9f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Iron, TileID.Lead)));
            Register(new OreDropRule(ItemID.SilverOre, 7f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Silver, TileID.Silver)));
            Register(new OreDropRule(ItemID.TungstenOre, 7f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Silver, TileID.Tungsten)));
            Register(new OreDropRule(ItemID.GoldOre, 6f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Gold, TileID.Gold)));
            Register(new OreDropRule(ItemID.PlatinumOre, 6f)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Gold, TileID.Platinum)));

            //煤:工业燃料,处处可得
            Register(new OreDropRule(ItemID.Coal, 8f));

            //邪恶矿:跟随世界邪恶类型,矿脉在场可跨界放行(醉酒世界两者并存)
            Register(new OreDropRule(ItemID.DemoniteOre, 6f, requiredPick: 55f)
                .SetWorldState(() => WorldGen.crimson ? OreWorldState.Absent : OreWorldState.Present)
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface || (ctx.Survey?.IsCorrupt ?? false)));
            Register(new OreDropRule(ItemID.CrimtaneOre, 6f, requiredPick: 55f)
                .SetWorldState(() => WorldGen.crimson ? OreWorldState.Present : OreWorldState.Absent)
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface || (ctx.Survey?.IsCrimson ?? false)));

            //陨石:纯矿脉驱动,附近真有陨石坑才会出
            Register(new OreDropRule(ItemID.Meteorite, 0f, requiredPick: 50f)
                .SetVein(0.9f, 200)
                .SetRare());

            //黑曜石与狱岩:地狱基线,矿脉加成
            Register(new OreDropRule(ItemID.Obsidian, 3f, requiredPick: 65f)
                .SetRelevance(ctx => ctx.Survey?.IsUnderworld ?? false)
                .SetVein(0.8f, 120));
            Register(new OreDropRule(ItemID.Hellstone, 7f, requiredPick: 65f)
                .SetRelevance(ctx => ctx.Survey?.IsUnderworld ?? false));

            //化石:沙漠地下
            Register(new OreDropRule(ItemID.FossilOre, 5f)
                .SetRelevance(ctx => ctx.Survey != null && ctx.Survey.IsDesert && !ctx.Survey.IsSurface)
                .SetVein(0.8f, 120)
                .SetRare());

            //宝石:地下基线极低,富集全靠真实宝石岩
            RegisterGem(ItemID.Amethyst, 1.2f);
            RegisterGem(ItemID.Topaz, 1.1f);
            RegisterGem(ItemID.Sapphire, 0.9f);
            RegisterGem(ItemID.Emerald, 0.8f);
            RegisterGem(ItemID.Ruby, 0.7f);
            RegisterGem(ItemID.Diamond, 0.5f);

            //困难模式三层:祭坛未破时报告显示"尚未探明"
            Register(new OreDropRule(ItemID.CobaltOre, 6f, requiredPick: 100f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Cobalt, TileID.Cobalt))
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface));
            Register(new OreDropRule(ItemID.PalladiumOre, 6f, requiredPick: 100f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Cobalt, TileID.Palladium))
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface));
            Register(new OreDropRule(ItemID.MythrilOre, 5f, requiredPick: 110f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Mythril, TileID.Mythril))
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface));
            Register(new OreDropRule(ItemID.OrichalcumOre, 5f, requiredPick: 110f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Mythril, TileID.Orichalcum))
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface));
            Register(new OreDropRule(ItemID.AdamantiteOre, 4f, requiredPick: 150f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Adamantite, TileID.Adamantite))
                .SetRelevance(ctx => ctx.Survey != null && (ctx.Survey.IsCavern || ctx.Survey.IsUnderworld)));
            Register(new OreDropRule(ItemID.TitaniumOre, 4f, requiredPick: 150f)
                .SetProgress(ctx => Main.hardMode)
                .SetWorldState(AltOre(() => WorldGen.SavedOreTiers.Adamantite, TileID.Titanium))
                .SetRelevance(ctx => ctx.Survey != null && (ctx.Survey.IsCavern || ctx.Survey.IsUnderworld)));

            //叶绿:困难模式丛林地下,必须装叶绿钻探模块
            Register(new OreDropRule(ItemID.ChlorophyteOre, 5f)
                .SetDrillLock()
                .SetProgress(ctx => Main.hardMode)
                .SetRelevance(ctx => ctx.Survey != null && ctx.Survey.IsJungle && !ctx.Survey.IsSurface)
                .SetVein(1.2f, 200));

            //夜明:月总后,必须装夜明钻探模块;矿脉加成让夜明田生效
            Register(new OreDropRule(ItemID.LunarOre, 4f)
                .SetDrillLock()
                .SetProgress(ctx => NPC.downedMoonlord)
                .SetVein(1.2f, 160));
        }

        private static void RegisterGem(int itemID, float baseWeight) {
            //宝石岩一块就是一颗,矿脉加成给高、上限给小
            Register(new OreDropRule(itemID, baseWeight)
                .SetRelevance(ctx => ctx.Survey != null && !ctx.Survey.IsSurface)
                .SetVein(2f, 40)
                .SetRare());
        }

        private static void LoadSpecialOres() {
            if (CWRRef.Has) {
                //嘉登残料:工业废土风味,处处低权重
                Register(new OreDropRule(CWRID.Item_DubiousPlating, 2.5f).SetRare());
                Register(new OreDropRule(CWRID.Item_MysteriousCircuitry, 2.5f).SetRare());
            }
        }

        public static void Register(OreDropRule rule) => DropRules.Add(rule);

        /// <summary>登记额外矿脉方块映射(供扩展/灾厄矿)</summary>
        public static void RegisterOreTile(int tileType, int itemID) => MiningSurvey.OreTileToItem[tileType] = itemID;

        /// <summary>
        /// 加权单次掷骰:在所有可产出条目中按权重抽一个。
        /// 与 <see cref="BuildReport"/> 使用同一套资格与权重,UI 展示的份额即真实概率
        /// </summary>
        public static bool TryRollDrop(in MiningContext ctx, UnifiedRandom rand, out int itemID) {
            itemID = 0;
            if (DropRules == null || DropRules.Count == 0) {
                return false;
            }

            float total = 0f;
            Span<float> weights = DropRules.Count <= 128 ? stackalloc float[DropRules.Count] : new float[DropRules.Count];
            for (int i = 0; i < DropRules.Count; i++) {
                weights[i] = 0f;
                if (DropRules[i].Evaluate(in ctx, out float weight) == OreGate.Open) {
                    weights[i] = weight;
                    total += weight;
                }
            }
            if (total <= 0f) {
                return false;
            }

            float roll = rand.NextFloat() * total;
            for (int i = 0; i < DropRules.Count; i++) {
                if (weights[i] <= 0f) {
                    continue;
                }
                roll -= weights[i];
                if (roll <= 0f) {
                    itemID = DropRules[i].ItemID;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 生成勘探报告:可产出条目按份额降序在前,被门控的条目按原因列在后
        /// </summary>
        public static List<OreReportEntry> BuildReport(in MiningContext ctx) {
            List<OreReportEntry> open = [];
            List<OreReportEntry> locked = [];
            if (DropRules == null) {
                return open;
            }

            float total = 0f;
            foreach (OreDropRule rule in DropRules) {
                OreGate gate = rule.Evaluate(in ctx, out float weight);
                if (gate == OreGate.Hidden) {
                    continue;
                }
                OreReportEntry entry = new() {
                    ItemID = rule.ItemID,
                    Gate = gate,
                    Share = weight,//先存原始权重,收尾统一归一化
                    VeinTiles = ctx.VeinTiles(rule.ItemID),
                    RequiredPick = rule.RequiredPick,
                };
                if (gate == OreGate.Open) {
                    total += weight;
                    open.Add(entry);
                }
                else {
                    locked.Add(entry);
                }
            }

            for (int i = 0; i < open.Count; i++) {
                OreReportEntry entry = open[i];
                entry.Share = total > 0f ? entry.Share / total : 0f;
                open[i] = entry;
            }
            open.Sort((a, b) => b.Share.CompareTo(a.Share));
            locked.Sort((a, b) => a.Gate.CompareTo(b.Gate));
            open.AddRange(locked);
            return open;
        }
    }
}
