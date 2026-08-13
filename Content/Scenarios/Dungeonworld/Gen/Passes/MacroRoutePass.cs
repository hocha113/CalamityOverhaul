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

            int spineLeft = DungeonworldMetrics.BorderThick;
            int spineRight = DungeonworldMetrics.Width - DungeonworldMetrics.BorderThick;
            LayerBand[] bands = DungeonworldMetrics.Bands;

            //每层一条横贯全宽的空脊走廊,净高6,室内墙保群系判定(F11/F13)
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
            //===垂直连接清单(§1.4:每相邻层对≥2条,现状记账)===
            //已落地:主竖井x1(全层贯穿,即本段)。次级通道全部缺席,其中
            //L1→L2楼梯井(L1井口房Stairhead已建)的隔离带穿透【裁决:记入Wave-2,本腿不做】——
            //理由:穿透落点需P30跨层协调预留(L2侧房间/禁室足印已按当前几何冻结,
            //现在开洞有切坏L2内容的风险),且用户即临QA,收尾腿不引入新跨层几何;
            //Wave-2实现时把穿透点开进P30占用登记,井底接L2脊或专用前室
            int shaftLeft = DungeonworldMetrics.ShaftLeft;
            int shaftRight = shaftLeft + DungeonworldMetrics.ShaftWidth;
            LayerBand l7 = bands[^1];
            for (int y = l1.SpineInteriorTop; y < l7.SpineFloorTop; y++) {
                ushort wall = DungeonworldMetrics.BandForRow(y)?.Wall ?? WallID.BlueDungeonUnsafe;
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
            progress.Set(0.9);

            //出生点=安全房地板正中(F25先例,spawnTile在GenPass里设)
            Main.spawnTileX = DungeonworldMetrics.SpawnX;
            Main.spawnTileY = l1.SpineFloorTop;
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] P20 MacroRoute carved={TileBrush.ClearWrites - clearBase}" +
                $" platforms={TileBrush.PlatformWrites} spawn=({Main.spawnTileX},{Main.spawnTileY})");
        }
    }
}
