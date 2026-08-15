using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //====================================================================
    //L4静态水体系统 + 两态水位机雏形(STRUCTURES §5.1-R1定论:两态脚本化)
    //
    //公理:湿舱段是"堰坎水密舱段"——水体只经构造性密封的实心边界与外界相邻,
    //一切开口(门/走道/栅洞)都在水面之上,或与同一水体连续(§2.4-④/§3.2-9)。
    //推论(本波裁决,记档):跨房连续水柱在该公理下不可密封(井底出口必在水面下),
    //故ROOMS-L4 §1"排水井满水态泳道"改为(a)组间干楼梯井+(b)房内自包含深潜井,
    //垂直泳感由蓄水大厅/深潜井的房内水柱承担——泄漏风险构造性归零。
    //
    //两态数据结构:两张"液体版图"不存全图位图(12M格浪费),按舱段存
    //"矩形+两态水面行"——版图=Σ舱段,一次性重写=逐舱段逐格赋LiquidAmount。
    //
    //耗时心算(写入注释供回归):全层水体约1.5万格,FillState为O(水体面积)毫秒级;
    //settle配方QuickWater限带扫描约200万格次+WaterCheck全图1200万格次×(2~11次,
    //构造完美时首轮numLiquid=0提前退出→典型2次),预期<2s,远内<3min全局预算(R5)。
    //====================================================================
    internal static class L4WaterWorks
    {
        //一个堰坎密封舱段:Area=水体包络(含内部实心障碍,填充时跳过),
        //水占[SurfaceRow,Area.Bottom)中的非实心格;AirPockets=气龛(潜水钟,填充豁免)
        internal sealed class Compartment
        {
            internal Rectangle Area;
            //满水态水面行(最顶水行);行号越小水越深
            internal int HighSurfaceRow;
            //排水态水面行;== Area.Bottom 表示排空
            internal int LowSurfaceRow;
            internal readonly List<Rectangle> AirPockets = [];
            internal string Name;
        }

        internal static readonly List<Compartment> Compartments = [];
        //当前应用的态:true=满水(生成期默认);两态切换的回放依据
        internal static bool HighState { get; private set; } = true;
        //主泵房机器锚(WaterLevelController TP挂点,STRUCTURES §4.1;
        //水位切换本身已由Machines\DungeonworldWaterGate接在阀杆上,此锚留给日后的泵机演出)
        internal static Point? PumpMachineAnchor;

        //每次生成/看样重算(ShouldSave=false回放制,镜像LayerPlans.Reset纪律)
        internal static void Reset() {
            Compartments.Clear();
            HighState = true;
            PumpMachineAnchor = null;
        }

        internal static Compartment Register(string name, Rectangle area, int highSurface, int lowSurface) {
            var c = new Compartment {
                Name = name, Area = area,
                HighSurfaceRow = highSurface, LowSurfaceRow = lowSurface,
            };
            Compartments.Add(c);
            return c;
        }

        //==================== 一次性重写:把登记舱段写成指定态的液体版图 ====================

        /// <summary>
        /// 逐舱段重写LiquidAmount:水面行以下非实心格=255,以上=0,气龛=0。
        /// 平台/链等非实心格照常持水(镜像WaterCheck的实心判据,WorldGen.cs L73417)。
        /// 返回写入的水格数。
        /// </summary>
        internal static int FillState(bool high) {
            HighState = high;
            int wet = 0;
            foreach (Compartment c in Compartments) {
                int surface = high ? c.HighSurfaceRow : c.LowSurfaceRow;
                for (int x = c.Area.Left; x < c.Area.Right; x++) {
                    for (int y = c.Area.Top; y < c.Area.Bottom; y++) {
                        if (!WorldGen.InWorld(x, y, 5)) {
                            continue;
                        }
                        Tile t = Main.tile[x, y];
                        //实心非平台格不持液体(与WaterCheck判据一致,防幽灵水)
                        if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                            continue;
                        }
                        bool inWater = y >= surface && !InPocket(c, x, y);
                        t.LiquidAmount = inWater ? byte.MaxValue : (byte)0;
                        if (inWater) {
                            t.LiquidType = LiquidID.Water;
                            wet++;
                        }
                    }
                }
            }
            return wet;
        }

        private static bool InPocket(Compartment c, int x, int y) {
            foreach (Rectangle p in c.AirPockets) {
                if (p.Contains(x, y)) {
                    return true;
                }
            }
            return false;
        }

        //==================== settle收尾(F23配方,WorldGen.cs L11725-11781"Settle Liquids"对源) ====================

        /// <summary>
        /// 原版settle配方的限带版。构造完美时它是空转保险(numLiquid=0首轮即退),
        /// 真正职责是抹平构造bug并让WaterCheck清掉实心格内液体/淹死WaterDeath家具。
        /// 时机:本层一切几何+家具冻结之后、水线paint之前;层入口内自包含执行
        /// (管线现无P70液体pass,若日后上P70,本方法可原样移交,见L4Content注)。
        /// </summary>
        internal static void SettleBand(LayerBand band) {
            //暗礁(对源核实):SettleWaterAt在WorldGen.gen期间会把"下落过的水"按
            //GenVars.waterLine转岩浆(Liquid.cs L173-183),子世界gen期WorldGen.gen=true
            //(SubworldSystem.cs L1193)而waterLine是主世界残值——必须先钝化两条线
            //(原版恐慌模式同款处理,Liquid.cs L969),用毕恢复
            int savedWaterLine = GenVars.waterLine;
            int savedLavaLine = GenVars.lavaLine;
            GenVars.waterLine = Main.maxTilesY;
            GenVars.lavaLine = Main.maxTilesY;

            Liquid.worldGenTilesIgnoreWater(ignoreSolids: true);
            //限带快速沉降(minY/maxY为QuickWater原生参数,Liquid.cs L103)
            Liquid.QuickWater(0, band.Top - 2, band.Bottom + 2);
            WorldGen.WaterCheck();
            int rounds = 0;
            int updates = 0;
            Liquid.quickSettle = true;
            //原版外层固定10轮;全世界液体只有本层且构造静定,numLiquid==0即提前收束(预算R5)
            while (rounds < 10) {
                rounds++;
                int active = Liquid.numLiquid + Terraria.LiquidBuffer.numLiquidBuffer;
                if (active == 0) {
                    break;
                }
                int budget = active * 5;
                while (Liquid.numLiquid > 0 && budget-- > 0) {
                    Liquid.UpdateLiquid();
                    updates++;
                }
                WorldGen.WaterCheck();
            }
            Liquid.quickSettle = false;
            Liquid.worldGenTilesIgnoreWater(ignoreSolids: false);
            GenVars.waterLine = savedWaterLine;
            GenVars.lavaLine = savedLavaLine;

            CWRMod.Instance.Logger.Info(
                $"[L4WaterWorks] settle收束 rounds={rounds} updates={updates}"
                + $" 残余numLiquid={Liquid.numLiquid}+buffer={Terraria.LiquidBuffer.numLiquidBuffer}");
        }

        /// <summary>
        /// 静定断言(fail loud,§3.1-2):带内液体必须全部落在登记舱段的当前水面下,
        /// 且无岩浆混入(waterLine暗礁的回归探针)。返回带内水格总数。
        /// </summary>
        internal static int AssertBandWater(LayerBand band) {
            int wet = 0, stray = 0, lava = 0;
            for (int x = DungeonworldMetrics.PlayLeft; x < DungeonworldMetrics.PlayRight; x++) {
                for (int y = band.Top; y < band.Bottom; y++) {
                    Tile t = Main.tile[x, y];
                    if (t.LiquidAmount == 0) {
                        continue;
                    }
                    wet++;
                    if (t.LiquidType != LiquidID.Water) {
                        lava++;
                    }
                    if (!InAnyCompartment(x, y)) {
                        stray++;
                    }
                }
            }
            if (stray > 0 || lava > 0) {
                CWRMod.Instance.Logger.Error(
                    $"[L4WaterWorks] 静定断言失败:舱段外游水{stray}格/非水液体{lava}格,责任=L4堰坎构造");
            }
            return wet;
        }

        private static bool InAnyCompartment(int x, int y) {
            foreach (Compartment c in Compartments) {
                int surface = HighState ? c.HighSurfaceRow : c.LowSurfaceRow;
                if (x >= c.Area.Left && x < c.Area.Right && y >= surface && y < c.Area.Bottom) {
                    return true;
                }
            }
            return false;
        }

        //==================== 两态切换(雏形:机制函数+数据;运行时TP机关归资产波) ====================

        /// <summary>
        /// 双水线痕+分带墙:几何与液体冻结后调用(paint/wall层,§3.2-6)。
        /// 排水态水面行==Area.Bottom(排空)时,下线刷在底行上一格——干舱也留"水曾经到过这"的黑线。
        /// </summary>
        internal static void PaintAging() {
            foreach (Compartment c in Compartments) {
                L4Palette.PaintWaterlineRow(c.Area.Left, c.Area.Right, c.HighSurfaceRow, L4Palette.HighLinePaint);
                int lowPaint = c.LowSurfaceRow >= c.Area.Bottom ? c.Area.Bottom - 1 : c.LowSurfaceRow;
                if (lowPaint != c.HighSurfaceRow && lowPaint >= c.Area.Top) {
                    L4Palette.PaintWaterlineRow(c.Area.Left, c.Area.Right, lowPaint, L4Palette.LowLinePaint);
                }
                int wallTop = System.Math.Max(c.Area.Top - 4, 0);
                L4Palette.BandWalls(c.Area.Left, c.Area.Right, wallTop, c.Area.Bottom, c.HighSurfaceRow);
            }
        }

        /// <summary>
        /// 一次性事务切换全层水位(R1:预计算版图+一次重写+手动settle,绝不物理模拟排水)。
        /// 排走的水去向叙事化(排入深渊带),不做守恒模拟。
        /// <br/>带settle的这一版只给生成期与看样入口用;运行时切换走
        /// <see cref="ApplyStateRuntime"/>(settle在运行时会卡秒级)。
        /// </summary>
        internal static void ApplyState(bool high, LayerBand band) {
            if (Compartments.Count == 0) {
                CWRMod.Instance.Logger.Warn("[L4WaterWorks] 无登记舱段,切换忽略");
                return;
            }
            int wet = FillState(high);
            SettleBand(band);
            CWRMod.Instance.Logger.Info(
                $"[L4WaterWorks] 水位切换→{(high ? "满水" : "排空")} 舱段={Compartments.Count} 水格={wet}");
        }

        /// <summary>
        /// 运行时切换:只重写液体版图,不跑settle。返回写入的水格数,无舱段登记时返回-1。
        /// <br/>为什么敢省掉settle:它是生成期的构造bug保险,内含全图WaterCheck(1200万格)
        /// 与多轮UpdateLiquid,秒级耗时——放在运行时就是几秒硬卡帧。而堰坎舱段本就是
        /// 构造性密封的静水(§2.4-④/§3.2-9),且子世界NormalUpdates=false让
        /// Liquid.UpdateLiquid根本不转(F16/F17),重写完即静定,没有东西会来推它。
        /// </summary>
        internal static int ApplyStateRuntime(bool high) {
            if (Compartments.Count == 0) {
                return -1;
            }
            return FillState(high);
        }
    }
}
