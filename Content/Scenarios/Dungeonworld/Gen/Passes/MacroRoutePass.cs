using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P20:每层空脊走廊+教堂占位安全房+主竖井(M0楼梯版)+出生点(§1.4/§5.2 M0)
    internal class MacroRoutePass : GenPass
    {
        public MacroRoutePass() : base("Dungeonworld MacroRoute", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "开凿层脊走廊与主竖井...";
            long clearBase = TileBrush.ClearWrites;

            int spineLeft = DungeonworldMetrics.PlayLeft;
            int spineRight = DungeonworldMetrics.PlayRight;
            LayerBand[] bands = DungeonworldMetrics.Bands;

            //每层一条横贯可达区的空脊走廊(钳制线外不开凿),净高6,室内墙保群系判定(F11/F13)
            for (int i = 0; i < bands.Length; i++) {
                LayerBand band = bands[i];
                TileBrush.CarveRect(spineLeft, band.SpineInteriorTop, spineRight, band.SpineFloorTop, band.Wall);
                progress.Set(0.4 * (i + 1) / bands.Length);
            }

            //教堂占位安全房,与L1脊共用地板行,脊从房中横穿
            LayerBand l1 = bands[0];
            int roomLeft = DungeonworldMetrics.SafeRoomLeft;
            int roomRight = roomLeft + DungeonworldMetrics.SafeRoomWidth;
            TileBrush.CarveRect(roomLeft, l1.SpineFloorTop - DungeonworldMetrics.SafeRoomHeight,
                roomRight, l1.SpineFloorTop, l1.Wall);

            //主竖井:从L1脊顶到L7脊地板,几何连续贯穿全部隔离带(§1.4)
            //L7地板行不挖,竖井底直接落在L7脊
            //
            //===垂直连接清单(§1.4:每相邻层对≥2条,Wave-2补全)===
            //1.主竖井x1:全层贯穿,即本段;
            //2.第二通道族x6:每个隔离带一口楼梯井式穿透(含Wave-1记账缺口L1→L2,
            //  井位钉在L1井口房Stairhead窗口近旁兑现其"口部预留"叙事),
            //  取位/禁带/足印预留/L7→深渊不开口裁决全文见VerticalLinks头注释,
            //  井身刻画在本pass下方之字平台段之后
            int shaftLeft = DungeonworldMetrics.ShaftLeft;
            int shaftRight = shaftLeft + DungeonworldMetrics.ShaftWidth;
            LayerBand l7 = bands[^1];
            for (int y = l1.SpineInteriorTop; y < l7.SpineFloorTop; y++) {
                ushort wall = DungeonworldMetrics.WallForRow(y);
                for (int x = shaftLeft; x < shaftRight; x++) {
                    TileBrush.ClearCell(x, y, wall);
                }
            }
            progress.Set(0.6);

            //竖井与L1..L6脊的交口铺全宽平台桥,徒步不断路,按▼穿透下落
            for (int i = 0; i < bands.Length - 1; i++) {
                TileBrush.PlatformRow(shaftLeft, shaftRight, bands[i].SpineFloorTop,
                    DungeonworldMetrics.PlatformFrameY);
            }

            //之字平台按段的下沿锚定向上铺,竖距4,左右交替各3宽
            //下落最多4行,上行跳跃4行(F2满跳约6.6格,留余量)
            int step = DungeonworldMetrics.ShaftStepRows;
            for (int i = 0; i < bands.Length - 1; i++) {
                int upperFloor = bands[i].SpineFloorTop;
                int lowerFloor = bands[i + 1].SpineFloorTop;
                for (int y = lowerFloor - step; y >= upperFloor + 2; y -= step) {
                    bool leftSide = ((y / step) & 1) == 0;
                    int platLeft = leftSide ? shaftLeft : shaftRight - 3;
                    TileBrush.PlatformRow(platLeft, platLeft + 3, y, DungeonworldMetrics.PlatformFrameY);
                }
            }
            progress.Set(0.85);

            //===第二通道族:每隔离带一口楼梯井(Wave-2,§1.4)===
            //取位是全管线第一组genRand消耗点(自上而下固定顺序,先于P30禁室定点,
            //R4随机流纪律);足印由P30 ReserveInto预留进相邻两带ctx.Grid
            VerticalLinks.PickAll();
            for (int i = 0; i < bands.Length - 1; i++) {
                int wellLeft = VerticalLinks.WellLeft[i];
                if (wellLeft < 0) {
                    //取位失败已在PickAll内fail loud,该层对退回仅主竖井
                    continue;
                }
                int wellRight = wellLeft + VerticalLinks.WellWidth;
                int upperFloor = bands[i].SpineFloorTop;
                int lowerFloor = bands[i + 1].SpineFloorTop;
                //井身:上层脊地板行(穿透)→下层脊地板行(不挖,井底即下层脊,镜像主竖井语义)
                for (int y = upperFloor; y < lowerFloor; y++) {
                    ushort wall = DungeonworldMetrics.WallForRow(y);
                    for (int x = wellLeft; x < wellRight; x++) {
                        TileBrush.ClearCell(x, y, wall);
                    }
                }
                //上口全宽平台桥:上层脊徒步不断路,按▼穿透下落(镜像竖井交口做法)
                TileBrush.PlatformRow(wellLeft, wellRight, upperFloor, DungeonworldMetrics.PlatformFrameY);
                //之字平台与主竖井同语法:竖距4上行可跳(F2),左右交替3宽
                for (int y = lowerFloor - step; y >= upperFloor + 2; y -= step) {
                    bool leftSide = ((y / step) & 1) == 0;
                    int platLeft = leftSide ? wellLeft : wellRight - 3;
                    TileBrush.PlatformRow(platLeft, platLeft + 3, y, DungeonworldMetrics.PlatformFrameY);
                }
            }
            progress.Set(0.95);

            //出生点=安全房地板正中(F25先例,spawnTile在GenPass里设)
            Main.spawnTileX = DungeonworldMetrics.SpawnX;
            Main.spawnTileY = l1.SpineFloorTop;
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] P20 MacroRoute carved={TileBrush.ClearWrites - clearBase}" +
                $" platforms={TileBrush.PlatformWrites} spawn=({Main.spawnTileX},{Main.spawnTileY})" +
                $" wells=[{VerticalLinks.Summary()}]");
        }
    }
}
