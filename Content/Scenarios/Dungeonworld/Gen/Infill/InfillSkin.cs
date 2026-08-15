using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //填充体系的层皮肤表:夹层带(P52)与封存副翼(P54)共用同一套几何,
    //换各层自己的砖/墙/家具/做旧漆。取值一律转引各 L#Palette 已核实的常量,
    //本表不自己认领任何新母题、不新造样式号(跨层母题裁决见 ROOMS-INDEX §3)。
    //
    //只给两个体系实际服务的五带配皮:L1/L2(副翼)、L4/L5(夹层)、L6(两者都吃)。
    //L3 已是全幅甲板制不缺填充,L7 悬空构图要求四周≥20格空隙(STRUCTURES §2.4-⑦),
    //两带一律 null=跳过,不编造用不上的行。
    //====================================================================
    internal sealed class InfillSkin
    {
        internal ushort Brick;
        internal ushort CrackedBrick;
        internal ushort WallBase;
        internal ushort WallSlab;
        internal ushort WallTiled;
        internal short PlatformFrameY;
        internal int DoorStyle;
        internal int TableStyle;
        internal int ChairStyle;
        internal int WorkBenchStyle;
        internal int CandleStyle;
        internal int CandelabraStyle;
        /// <summary>tile42 挂灯样式;-1=本层不认领挂灯族,退回烛台</summary>
        internal int LanternStyle;
        internal int ChestStyle;
        /// <summary>副翼大奖箱:锁金箱 style 2(各层 ChestLockedGold 同值,M4战利品表对位前的占位)</summary>
        internal int ChestRewardStyle = 2;
        internal int PotStyleMin;
        /// <summary>Next 上界(不含)</summary>
        internal int PotStyleMax;
        /// <summary>做旧签名漆(INDEX §3:每层一种,互不借用)</summary>
        internal byte AgePaint;
        /// <summary>
        /// 基调层染漆与粗块覆盖率(INDEX §3"基调层染"行)。各层主内容在P50里已经把
        /// 自己那片洗过了,填充区是P52/P54之后才凿出来的,不补这一道就会是一片没上色的生砖。
        /// 覆盖率0=本层不染(L1素蓝/L2素粉)。
        /// </summary>
        internal byte TintPaint;
        internal int TintCoverage;
        /// <summary>块散列盐,让填充区的墙变体斑形与各层主内容错开</summary>
        internal int PatchSalt;
        /// <summary>
        /// 本层可否在<b>玩家踩得到的面</b>上用裂砖。裂砖在原版是"假地板"——踩碎坠落
        /// (STRUCTURES F31),所以它同时是陷阱母题:认领表判给 L5/L6,L1/L3/L4/L7 禁用,
        /// L2 只留"教学首现单段"那一处。禁用层的旧损感改由本层砖+做旧漆表达。
        /// <para/>天花与过梁位不受此限——那里的裂砖只是纹理,是 §3.2-6 明许的做旧手段之一。
        /// </summary>
        internal bool AllowCrackedFloor;

        //与 DungeonworldMetrics.Bands 同索引
        private static readonly InfillSkin[] ByBand = [
            //L1教堂区:做旧签名=蜡泪(白漆);无挂灯族,烛台顶班
            new() {
                Brick = L1Style.Brick,
                CrackedBrick = TileID.CrackedBlueDungeonBrick,
                WallBase = L1Style.Wall,
                WallSlab = L1Style.WallSlab,
                WallTiled = WallID.BlueDungeonTileUnsafe,
                PlatformFrameY = DungeonworldMetrics.PlatformFrameY,
                DoorStyle = L1Style.StyleDoor,
                TableStyle = L1Style.StyleTable,
                ChairStyle = L1Style.StyleChair,
                WorkBenchStyle = L1Style.StyleWorkbench,
                CandleStyle = L1Style.StyleCandle,
                CandelabraStyle = L1Style.StyleCandelabra,
                LanternStyle = -1,
                ChestStyle = 2,
                PotStyleMin = 10, PotStyleMax = 13,
                AgePaint = PaintID.WhitePaint,
                PatchSalt = 0x1A31,
                AllowCrackedFloor = false,
            },
            //L2牢狱层:做旧签名=锈渍垂痕(棕漆);挂灯=链灯笼
            new() {
                Brick = L2Palette.Brick,
                CrackedBrick = L2Palette.CrackedBrick,
                WallBase = L2Palette.WallBase,
                WallSlab = L2Palette.WallSlab,
                WallTiled = WallID.PinkDungeonTileUnsafe,
                PlatformFrameY = L2Palette.PlatformFrameY,
                DoorStyle = L2Palette.DoorStyle,
                TableStyle = L2Palette.TableStyle,
                ChairStyle = L2Palette.ChairStyle,
                WorkBenchStyle = L2Palette.WorkBenchStyle,
                CandleStyle = L2Palette.CandleStyle,
                CandelabraStyle = L2Palette.CandelabraStyle,
                LanternStyle = L2Palette.LanternChainStyle,
                ChestStyle = L2Palette.ChestBarrelStyle,
                PotStyleMin = L2Palette.PotStyleMin, PotStyleMax = L2Palette.PotStyleMax,
                AgePaint = L2Palette.RustPaint,
                PatchSalt = 0x2C07,
                //L2的裂砖只许"教学首现单段"那一处,副翼不再借用
                AllowCrackedFloor = false,
            },
            //L3大档案馆:甲板制已吃满纵深,不参与填充
            null,
            //L4水牢:做旧签名=苔藓(深绿漆);挂灯=油布壁灯
            new() {
                Brick = L4Palette.Brick,
                CrackedBrick = L4Palette.CrackedBrick,
                WallBase = L4Palette.WallBase,
                WallSlab = L4Palette.WallSlab,
                WallTiled = L4Palette.WallTiled,
                PlatformFrameY = L4Palette.PlatformFrameY,
                DoorStyle = L4Palette.DoorStyle,
                TableStyle = L4Palette.TableStyle,
                ChairStyle = L4Palette.ChairStyle,
                WorkBenchStyle = L4Palette.WorkBenchStyle,
                CandleStyle = L4Palette.CandleStyle,
                CandelabraStyle = L4Palette.CandelabraStyle,
                LanternStyle = L4Palette.LanternSconceStyle,
                ChestStyle = L4Palette.ChestBarrelStyle,
                PotStyleMin = L4Palette.PotStyleMin, PotStyleMax = L4Palette.PotStyleMax,
                AgePaint = L4Palette.MossPaint,
                //沼绿=绿砖本色+苔藓深绿漆
                TintPaint = L4Palette.MossPaint, TintCoverage = 30,
                PatchSalt = 0x4E55,
                AllowCrackedFloor = false,
            },
            //L5万骨窖:做旧签名=尘白;挂灯=骨灯笼
            new() {
                Brick = L5Palette.Brick,
                CrackedBrick = L5Palette.CrackedBrick,
                WallBase = L5Palette.WallBase,
                WallSlab = L5Palette.WallSlab,
                WallTiled = L5Palette.WallTiled,
                PlatformFrameY = L5Palette.PlatformPink,
                DoorStyle = L2Palette.DoorStyle,
                TableStyle = L5Palette.TableBone,
                ChairStyle = L5Palette.ChairBone,
                WorkBenchStyle = L5Palette.WorkBenchBone,
                CandleStyle = L5Palette.CandlePink,
                CandelabraStyle = L5Palette.CandelabraPink,
                LanternStyle = L5Palette.LanternBone,
                ChestStyle = L5Palette.ChestLockedGold,
                PotStyleMin = L5Palette.PotStyleMin, PotStyleMax = L5Palette.PotStyleMax,
                AgePaint = L5Palette.DustPaint,
                //骨白=白漆26
                TintPaint = L5Palette.DustPaint, TintCoverage = 26,
                PatchSalt = 0x5B93,
                AllowCrackedFloor = true,
            },
            //L6铸造机关层:做旧签名=焦痕油渍(黑漆);挂灯=黄铜灯笼
            new() {
                Brick = L6Palette.Brick,
                CrackedBrick = L6Palette.CrackedBrick,
                WallBase = L6Palette.WallBase,
                WallSlab = L6Palette.WallSlab,
                WallTiled = L6Palette.WallTiled,
                PlatformFrameY = L6Palette.PlatformFrameY,
                DoorStyle = L6Palette.DoorStyle,
                TableStyle = L6Palette.TableStyle,
                ChairStyle = L6Palette.ChairStyle,
                WorkBenchStyle = L6Palette.WorkBenchStyle,
                CandleStyle = L6Palette.CandleStyle,
                CandelabraStyle = L6Palette.CandelabraStyle,
                LanternStyle = L6Palette.LanternBrassStyle,
                ChestStyle = L6Palette.ChestBarrelStyle,
                PotStyleMin = L6Palette.PotStyleMin, PotStyleMax = L6Palette.PotStyleMax + 1,
                AgePaint = L6Palette.TarPaint,
                //炉锈橙=深橙漆覆盖45(与做旧的焦痕黑分开:层染管底色,焦痕管痕迹)
                TintPaint = L6Palette.RustPaint, TintCoverage = 45,
                PatchSalt = 0x6D21,
                AllowCrackedFloor = true,
            },
            //L7倒吊教堂:悬空构图要留空,不参与填充
            null,
        ];

        /// <summary>按层带索引取皮肤;返回 null=该带不参与填充</summary>
        internal static InfillSkin For(int bandIndex)
            => bandIndex >= 0 && bandIndex < ByBand.Length ? ByBand[bandIndex] : null;

        /// <summary>本层地牢墙族,层染/做旧只认这三种,避开彩窗与栅栏</summary>
        internal ushort[] WallFamily => [WallBase, WallSlab, WallTiled];

        internal ushort[] BrickFamily => [Brick, CrackedBrick];
    }
}
