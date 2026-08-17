using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z1
{
    //Z1 墙脚带：出生锚点/登出终端 + 规整浮空板 + 浅层接入机房
    //教学密度，低威胁；M3 目录扩容：接入区/废弃中继阵列/规整机房
    internal static class Z1Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            PlaceAnchors();
            //M3 地表目录：接入亭（教学动线地标）+ 废弃中继阵列（剪影）
            int pods = Z1Rooms.BuildShelterPods(ctx, 2);
            int pylons = Z1Rooms.BuildDeadPylonArrays(ctx, 2);
            OldNetZoneCommon.PlaceFloatingSlabs(
                OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols + 30,
                ctx.Area.Right - 20, 55, 95, Z1Style.FloorBrick);
            int rooms = OldNetZoneCommon.HangRoomsForBand(ctx, 1,
                Z1Style.RoomBrick, Z1Style.RoomWall, roomsPerLanding: 2, nodeChance: 0.6f);
            CWRMod.Instance.Logger.Info(
                $"[OldNet] Z1 rooms={rooms} pods={pods} pylons={pylons} graphConnected={ctx.Graph.IsConnected()}");
        }

        //出生点与登出终端（P30已预留足印，这里落tile）
        private static void PlaceAnchors() {
            int[] floorTop = OldNetPlans.FloorTop;
            Main.spawnTileX = OldNetMetrics.SpawnX;
            Main.spawnTileY = floorTop[OldNetMetrics.SpawnX];

            int terminalX = OldNetMetrics.LogoutX;
            int terminalY = floorTop[terminalX] - 1;
            if (!OldNetNodeBudget.WriteNodeTile(terminalX, terminalY,
                ModContent.TileType<OldNetLogoutTerminalTile>())) {
                CWRMod.Instance.Logger.Warn($"[OldNet] 登出终端落位失败@({terminalX},{terminalY})");
            }
        }
    }
}
