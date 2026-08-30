using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //层带,自上而下固定行数堆叠,区间半开[Top,Bottom)
    internal readonly struct LayerBand
    {
        internal readonly string Name;
        internal readonly int Top;
        internal readonly int Bottom;
        internal readonly ushort Brick;
        internal readonly ushort Wall;

        internal LayerBand(string name, int top, int rows, ushort brick, ushort wall) {
            Name = name;
            Top = top;
            Bottom = top + rows;
            Brick = brick;
            Wall = wall;
        }

        /// <summary>层脊走廊内膛顶行(含)</summary>
        internal int SpineInteriorTop => Bottom - DungeonworldMetrics.SpineReserveBelow - DungeonworldMetrics.SpineClearance;
        /// <summary>层脊走廊地板顶行,玩家立足行</summary>
        internal int SpineFloorTop => Bottom - DungeonworldMetrics.SpineReserveBelow;
    }

    //生成常量集中声明(蓝图STRUCTURES §1.2层带表/§2.5走廊语法/§4.4坐标约定)
    //2026-08-14用户拍板扩容1000x1600→2000x6000:人体尺度层(L1/L2)不放大,
    //新增深度大头给中深层L4/L5/L6与弹性层L3,深渊带加厚兼作深渊过渡
    //
    //原版高度阈值排查结论(6000高,逐条对TML源码核实,全部"层带避开",OnLoad无需改字段):
    //1.Main.UnderworldLayer=maxTilesY-200=5800(Main.cs L3246):地狱音乐/ZoneUnderworldHeight/
    //  地狱背景盒均要求y>5800,可达最深点L7脊地板5594行,余量206行,深渊带实心不可达
    //2.太空低重力:SubLib IL补丁(SubworldLibrary.cs L104)在!NormalUpdates子世界把
    //  Player.Update重力局部量整段替换为Subworld.GetGravity(默认1),原版公式被绕过;
    //  且原版公式(Player.cs L21426)阈值≈60+10*(maxTilesX/4200)^2行,只覆盖顶部边界附近
    //3.深度计(Main.cs L45098):英尺=(y-worldSurface)*2,"地狱"标签要y>5796不可达;
    //  行78以上显示"太空"字样(num25公式按worldSurface折算),L1上半段纯装饰性误标
    //4.背景切换:地下/洞穴背景按worldSurface(55)/rockLayer(222)切换,地狱背景绘制条件
    //  屏幕底>5800*16(Main.cs L51368),深处屏幕底最多~5630行不触发,SubLib hideUnderworld兜底
    //5.ZoneSkyHeight=y≤worldSurface*0.35=行19(Player.cs L14480),天空缓冲带上部少量误判,M0可接受
    //6.小地图渲染目标网格(2026-08-30补审,原排查漏项):原版mapTarget[5,2]单元2000x1800
    //  纵向只盖3600行,行≥3600的分段令DrawToMap_Section/checkMap/DrawMap索引越界,进世界数秒必崩;
    //  6000高需4行网格,由共享的SubworldMapGrid在OnLoad扩容/Update保养/回主世界收缩,
    //  高度另须被段高150整除(6000✓,Hadalworld为此从5000改5100)
    internal static class DungeonworldMetrics
    {
        internal const int Width = 2000;
        internal const int Height = 6000;
        //世界四周实心边界厚度(顶部天空带封口用;左右两侧的硬界是下方PlayLeft/PlayRight)
        internal const int BorderThick = 8;

        //原版玩家钳制(Player.BordersMovement):距世界边缘640+16px≈41格的一圈
        //地图上看得见却永远进不去。上下两侧天然安全(天空带60行纯背景/深渊带400行实心),
        //左右两侧必须让渡:各封实42列,水平开凿/落位/审计一律用[PlayLeft,PlayRight)
        //半开区间,钳制线外保持骨架实心不留废几何(P80死区审计兜底)
        internal const int PlayLeft = 42;
        internal const int PlayRight = Width - PlayLeft;

        //天空缓冲带[0,SkyRows),M1钟楼尖顶探入
        internal const int SkyRows = 60;
        //层间隔离带,只有登记过的垂直通道可穿透
        internal const int SeparatorRows = 12;
        //深渊过渡+地狱判定带,底部200行是UnderworldLayer(F21),上部200行留给
        //日后深渊演出,M0保持实心;加厚到400让L7地板(5594)避开5800线足200+行
        internal const int AbyssRows = 400;

        //worldSurface压到天空缓冲带底,全部层带判"地下"(F11/§1.3)
        //扩容后数值不变:天空带/L1/L2行数未动,55仍落在缓冲带底
        internal const int WorldSurfaceRow = 55;

        //各层行数预算,L3=弹性层吸收世界高度余量(§1.2)
        //重分配理由:L1/L2是房间尺度玩法层保持150,纵深探索大头压给L4-L6
        //(水牢管廊/万骨窖坑道/铸造机关串天然吃纵深),L7是Boss舞台只微放大
        internal const int L1Rows = 150;
        internal const int L2Rows = 150;
        internal const int L4Rows = 1000;
        internal const int L5Rows = 1400;
        internal const int L6Rows = 1200;
        internal const int L7Rows = 220;
        internal const int L3Rows = Height - SkyRows - AbyssRows - 6 * SeparatorRows
            - L1Rows - L2Rows - L4Rows - L5Rows - L6Rows - L7Rows;

        //主干道净高(§2.5)
        internal const int SpineClearance = 6;
        //脊地板2厚+带底余量4
        internal const int SpineReserveBelow = 6;

        internal const int SpawnX = Width / 2;

        //主竖井,x在教堂后殿侧(出生点右58格),由SpawnX推导随宽度自适应(§1.4)
        internal const int ShaftLeft = SpawnX + 58;
        internal const int ShaftWidth = 5;
        //之字平台竖距,≤5保证可上行(F2满跳约6.6格)
        internal const int ShaftStepRows = 4;
        //蓝地牢平台样式(RESEARCH §1.1d-6,墙7配frameY=108)
        internal const short PlatformFrameY = 108;

        //教堂占位安全房(L1正中,居中于SpawnX),M0纯矩形壳
        internal const int SafeRoomWidth = 44;
        internal const int SafeRoomHeight = 16;
        internal const int SafeRoomLeft = SpawnX - SafeRoomWidth / 2;

        //===M1工程机器常量(§2.5走廊语法/§3.2退化对照表)===
        //房间外壳厚度,单格墙是"单格缝隙"退化温床(§3.2-5)
        internal const int RoomShellThick = 2;
        //房间落位间距padding,占用栅格预留时外扩(§3.2-3)
        internal const int RoomPadding = 2;
        //支线走廊净高(底线3=F1只许低威胁区,标准4)
        internal const int CorridorClearance = 4;
        //坡道最大爬升,超过改楼梯井(§2.5:连续爬升>10格改楼梯井,取8留余量)
        internal const int RampMaxRise = 8;
        //楼梯井净宽(§2.5竖井净宽3)
        internal const int StairWellWidth = 3;

        internal static readonly LayerBand[] Bands;
        //rockLayer设在L2顶附近(§1.3)
        internal static readonly int RockLayerRow;

        static DungeonworldMetrics() {
            Bands = new LayerBand[7];
            int cursor = SkyRows;
            int index = 0;
            void Add(string name, int rows, ushort brick, ushort wall) {
                Bands[index++] = new LayerBand(name, cursor, rows, brick, wall);
                cursor += rows + SeparatorRows;
            }
            //层带砖/墙主调=各层L#Palette已声明的基调,此处是骨架浇筑、层脊走廊、主竖井、
            //隔离带井这些"房间之外"几何的取色源(2026-08-15:此前七带全写死蓝砖,
            //导致绿水牢/粉万骨窖的主干道也是蓝的,层色只剩房间外壳两格厚)。
            //改层色必须与对应L#Palette同步;原版地牢墙7/8/9/94~99九种全在Main.wallDungeon里
            //(Main.cs L10462-10470),换绿/粉不影响ZoneDungeon判定与刷怪
            Add("L1教堂区", L1Rows, TileID.BlueDungeonBrick, WallID.BlueDungeonUnsafe);
            Add("L2牢狱层", L2Rows, TileID.PinkDungeonBrick, WallID.PinkDungeonUnsafe);
            Add("L3大档案馆", L3Rows, TileID.BlueDungeonBrick, WallID.BlueDungeonUnsafe);
            Add("L4水牢", L4Rows, TileID.GreenDungeonBrick, WallID.GreenDungeonUnsafe);
            Add("L5万骨窖", L5Rows, TileID.PinkDungeonBrick, WallID.PinkDungeonSlabUnsafe);
            Add("L6铸造机关层", L6Rows, TileID.BlueDungeonBrick, WallID.BlueDungeonTileUnsafe);
            Add("L7倒吊教堂", L7Rows, TileID.BlueDungeonBrick, WallID.BlueDungeonTileUnsafe);
            //最后一层下方是深渊带而非隔离带
            cursor -= SeparatorRows;
            if (cursor + AbyssRows != Height) {
                throw new System.InvalidOperationException(
                    $"[Dungeonworld] 层带行数总和{cursor + AbyssRows}与Height{Height}不符");
            }
            RockLayerRow = Bands[1].Top;
        }

        internal static LayerBand? BandForRow(int y) {
            foreach (LayerBand band in Bands) {
                if (y >= band.Top && y < band.Bottom) {
                    return band;
                }
            }
            return null;
        }

        /// <summary>行→砖:带内取本带,带外(天空/隔离/深渊)按 <see cref="OwnerBand"/> 归属</summary>
        internal static ushort BrickForRow(int y) => (BandForRow(y) ?? OwnerBand(y)).Brick;

        /// <summary>行→墙:同 <see cref="BrickForRow"/> 的归属规则,竖井穿隔离带时自然见到砖色交接</summary>
        internal static ushort WallForRow(int y) => (BandForRow(y) ?? OwnerBand(y)).Wall;

        /// <summary>
        /// 带外行的取色归属:隔离带以中线切开,上半归上层、下半归下层
        /// 12行隔离带因此是一道真过渡带而不是断层(§1.2);
        /// 天空缓冲带归L1,深渊带归L7。
        /// </summary>
        private static LayerBand OwnerBand(int y) {
            if (y < Bands[0].Top) {
                return Bands[0];
            }
            for (int i = 0; i < Bands.Length - 1; i++) {
                int gapTop = Bands[i].Bottom;
                int gapBottom = Bands[i + 1].Top;
                if (y >= gapTop && y < gapBottom) {
                    return y < gapTop + (gapBottom - gapTop) / 2 ? Bands[i] : Bands[i + 1];
                }
            }
            return Bands[^1];
        }
    }
}
